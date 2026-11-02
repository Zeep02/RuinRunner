"""Loopback-only response loss fixture: forward first, commit upstream, then drop one response.
No credentials, request bodies or response bodies are logged or written.
"""
import argparse, http.client, http.server, json, pathlib, socket, threading, time

parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=5187);parser.add_argument('--campaign',action='store_true');args=parser.parse_args()
lock=threading.Lock();dropped=set();records=[]
root=pathlib.Path(__file__).resolve().parents[2]
report=root/'Backend/Validation'/('response-loss-proxy-'+time.strftime('%Y%m%d-%H%M%S')+'.json')
def record(value):
    with lock:
        records.append(value);report.parent.mkdir(parents=True,exist_ok=True)
        report.write_text(json.dumps(dict(method='forward to real API then close client socket; no body/token logging',events=records),indent=2),encoding='utf-8')
class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version='HTTP/1.1'
    def log_message(self,*_): pass
    def do_GET(self): self.forward()
    def do_POST(self): self.forward()
    def do_PUT(self): self.forward()
    def forward(self):
        length=int(self.headers.get('Content-Length','0'))
        if length>65536:self.send_error(413);return
        body=self.rfile.read(length) if length else None
        headers={k:v for k,v in self.headers.items() if k.lower() not in ('host','connection','transfer-encoding','content-length')}
        connection=http.client.HTTPConnection('127.0.0.1',5186,timeout=15)
        try:
            connection.request(self.command,self.path,body,headers)
            upstream=connection.getresponse();data=upstream.read();status=upstream.status
            key='create' if self.command=='POST' and self.path=='/v1/profiles' else 'settle' if self.command=='POST' and self.path.endswith('/settle') else None
            if args.campaign and self.command=='POST':
                if self.path.endswith('/trades'):key='trade'
                elif self.path.endswith('/rewards'):key='reward'
            drop=False
            with lock:
                if key and key not in dropped and 200<=status<300:dropped.add(key);drop=True
            if drop:
                record(dict(action='dropped-after-upstream-commit',operation=key,status=status))
                self.close_connection=True
                try:self.connection.shutdown(socket.SHUT_RDWR)
                except OSError:pass
                self.connection.close();return
            self.send_response(status)
            self.send_header('Content-Type',upstream.getheader('Content-Type','application/json'))
            self.send_header('Content-Length',str(len(data)));self.send_header('Connection','close');self.end_headers();self.wfile.write(data);self.close_connection=True
        except (OSError,http.client.HTTPException):
            record(dict(action='upstream-unavailable'))
            try:self.send_error(503)
            except OSError:pass
        finally:connection.close()
server=http.server.ThreadingHTTPServer(('127.0.0.1',args.port),Handler)
print(json.dumps(dict(status='ready',url=f'http://127.0.0.1:{args.port}',report=str(report))),flush=True)
server.serve_forever()
