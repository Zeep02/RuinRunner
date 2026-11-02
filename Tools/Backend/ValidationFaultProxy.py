"""Dedicated 5200 -> 5197 response-loss fixture; never forwards to another origin.

Only one initially empty guest can enroll per process. Credentials and message
bodies live in memory only. The evidence schema deliberately contains neither.
"""
import argparse
import datetime as dt
import http.client
import http.server
import json
import os
from pathlib import Path
import re
import socket
import threading
import time
import uuid

LISTEN_PORT = 5200
UPSTREAM_PORT = 5197
MAX_BYTES = 1024 * 1024
MUTATION_PATH = re.compile(r"^/v1/runs/([0-9a-fA-F-]{36})/(trades|settle)$")


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def upstream(method, path, body=None, token=None):
    headers = {"Content-Type": "application/json", "Connection": "close"}
    if token:
        headers["Authorization"] = token
    connection = http.client.HTTPConnection("127.0.0.1", UPSTREAM_PORT, timeout=20)
    try:
        connection.request(method, path, body, headers)
        response = connection.getresponse()
        content = response.read(MAX_BYTES + 1)
        if len(content) > MAX_BYTES:
            raise ValueError("upstream_response_size")
        return response.status, content
    finally:
        connection.close()


class State:
    def __init__(self, path, enabled, settlement_status="any", drop_delay_seconds=0):
        self.path = path
        self.lock = threading.RLock()
        self.tokens = set()
        self.player = None
        self.value = {"schema": 1, "startedUtc": utc(), "normalGameplay": False,
                      "listen": "http://127.0.0.1:5200/", "upstream": "http://127.0.0.1:5197/",
                      "faultsEnabled": enabled, "settlementStatus": settlement_status,
                      "dropDelaySeconds": drop_delay_seconds, "freshGuestEnrolled": False,
                      "requestCount": 0, "rejectedRequests": 0,
                      "dropped": {"buy": None, "settle": None}, "operations": []}
        if path.exists():
            raise ValueError("A new state file is required; previous evidence is preserved.")
        self.save()

    def save(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.path.with_suffix(".tmp")
        temporary.write_text(json.dumps(self.value, indent=2), encoding="utf-8")
        os.replace(temporary, self.path)

    def count(self, rejected=False):
        with self.lock:
            if rejected:
                self.value["rejectedRequests"] += 1
            else:
                self.value["requestCount"] += 1
            self.save()

    def enroll(self, auth):
        # Hold enrollment until the existing-profile check finishes so two fresh
        # callers cannot both become the one test account for this process.
        with self.lock:
            player, token = auth.get("playerId"), auth.get("accessToken")
            if not isinstance(player, str) or not isinstance(token, str):
                return False
            if self.player is not None and self.player != player:
                return False
            if self.player is None:
                status, body = upstream("GET", "/v1/profiles", token="Bearer " + token)
                if status != 200 or json.loads(body).get("profiles") != []:
                    return False
                self.player = player
            self.tokens.add("Bearer " + token)
            self.value["freshGuestEnrolled"] = True
            self.save()
            return True

    def permits(self, token):
        with self.lock:
            return token in self.tokens

    def record_operation(self, action, operation, status, committed):
        with self.lock:
            record = next((x for x in self.value["operations"]
                           if x["operationId"] == operation and x["action"] == action), None)
            if record is None:
                record = {"operationId": operation, "action": action, "requests": 0,
                          "droppedResponses": 0, "upstreamStatuses": []}
                self.value["operations"].append(record)
            record["requests"] += 1
            record["upstreamStatuses"].append(status)
            drop = bool(committed and self.value["faultsEnabled"] and self.value["dropped"][action] is None)
            if drop:
                record["droppedResponses"] += 1
                self.value["dropped"][action] = operation
            # Durable evidence exists before deliberately closing the client socket.
            self.save()
            return drop

    def snapshot(self):
        with self.lock:
            return json.dumps(self.value).encode("utf-8")


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *_):
        pass

    def do_GET(self):
        self.forward()

    def do_POST(self):
        self.forward()

    def do_PUT(self):
        self.forward()

    def reply(self, status, content):
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(content)
        self.close_connection = True

    def reject(self, status, code):
        self.server.state.count(rejected=True)
        self.reply(status, json.dumps({"code": code, "message": "Isolated validation proxy rejected this request."}).encode())

    def forward(self):
        state = self.server.state
        try:
            if self.command == "GET" and self.path == "/__faults/status":
                self.reply(200, state.snapshot())
                return
            state.count()
            if self.path not in ("/health/live", "/health/ready") and not self.path.startswith("/v1/"):
                self.reject(400, "proxy_path_rejected")
                return
            if "?" in self.path or "#" in self.path or self.headers.get("Transfer-Encoding"):
                self.reject(400, "proxy_request_rejected")
                return
            length = int(self.headers.get("Content-Length", "0"))
            if length < 0 or length > MAX_BYTES:
                self.reject(413, "proxy_request_size")
                return
            body = self.rfile.read(length) if length else None
            token = self.headers.get("Authorization")
            login = self.command == "POST" and self.path == "/v1/auth/guest"
            health = self.command == "GET" and self.path in ("/health/live", "/health/ready")
            if not login and not health and not state.permits(token):
                self.reject(403, "proxy_unenrolled_guest")
                return
            status, response = upstream(self.command, self.path, body, token)
            if login and status == 200 and not state.enroll(json.loads(response)):
                self.reject(403, "proxy_fresh_guest_required")
                return
            match = MUTATION_PATH.fullmatch(self.path) if self.command == "POST" else None
            if match:
                request = json.loads(body or b"{}")
                action = "settle" if match[2] == "settle" else "buy" if request.get("action") == "buy" else None
                operation = request.get("operationId")
                try:
                    operation = str(uuid.UUID(operation))
                except (ValueError, TypeError, AttributeError):
                    operation = None
                if action and operation:
                    receipt = json.loads(response) if status == 200 else {}
                    committed = (receipt.get("operationId") == operation
                                 and receipt.get("economy", {}).get("runId") == match[1]) if action == "buy" else (
                                     receipt.get("run", {}).get("runId") == match[1]
                                     and receipt.get("run", {}).get("status") in ("abandoned", "defeat", "victory"))
                    if action == "settle" and state.value["settlementStatus"] == "victory":
                        committed = committed and receipt.get("run", {}).get("status") == "victory"
                    if state.record_operation(action, operation, status, status == 200 and committed):
                        # Withhold the entire committed response before dropping
                        # the socket. A bounded launcher can stop its own player
                        # while that request is still unresolved, rather than
                        # racing a retry on the player's next 240fps frame.
                        time.sleep(state.value["dropDelaySeconds"])
                        self.close_connection = True
                        try:
                            self.connection.shutdown(socket.SHUT_RDWR)
                        except OSError:
                            pass
                        self.connection.close()
                        return
            self.reply(status, response)
        except (OSError, http.client.HTTPException, ValueError, KeyError, TypeError):
            # Error text from libraries may contain headers/body fragments. Emit
            # a fixed code only, and never use BaseHTTPRequestHandler trace logs.
            try:
                self.reply(503, b'{"code":"proxy_upstream_unavailable","message":"Validation upstream unavailable."}')
            except OSError:
                pass


class Server(http.server.ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def handle_error(self, request, client_address):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--state-file", required=True)
    parser.add_argument("--enable-faults", action="store_true")
    parser.add_argument("--settlement-status", choices=("any", "victory"), default="any")
    parser.add_argument("--drop-delay-seconds", type=float, default=0)
    args = parser.parse_args()
    if not 0 <= args.drop_delay_seconds <= 10:
        raise ValueError("Drop delay must be between zero and ten seconds.")
    path = Path(args.state_file).resolve()
    allowed = (Path(__file__).resolve().parents[2] / "Backend/Validation/FaultProxy").resolve()
    if not path.is_relative_to(allowed) or path.suffix != ".json":
        raise ValueError("State must be new evidence beneath Backend/Validation/FaultProxy.")
    # Bind before creating evidence; a failed launch never replaces a running proxy.
    server = Server(("127.0.0.1", LISTEN_PORT), Handler)
    server.state = State(path, args.enable_faults, args.settlement_status, args.drop_delay_seconds)
    print(json.dumps({"status": "ready", "listen": LISTEN_PORT, "upstream": UPSTREAM_PORT,
                      "faultsEnabled": args.enable_faults, "evidence": str(path)}), flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
