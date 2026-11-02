"""Loopback-only evidence viewer with byte ranges for native video seeking."""
import argparse
import os
import re
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

p = argparse.ArgumentParser()
p.add_argument('directory')
p.add_argument('--port', type=int, default=8893)
a = p.parse_args()

class EvidenceHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=a.directory, **kwargs)

    def end_headers(self):
        self.send_header('Accept-Ranges', 'bytes')
        super().end_headers()

    def send_head(self):
        self.remaining = None
        value = self.headers.get('Range')
        path = self.translate_path(self.path)
        if not value or not os.path.isfile(path):
            return super().send_head()
        match = re.fullmatch(r'bytes=(\d+)-(\d*)', value)
        size = os.path.getsize(path)
        if not match or int(match[1]) >= size:
            self.send_error(416)
            return None
        start = int(match[1])
        end = min(size - 1, int(match[2]) if match[2] else size - 1)
        if end < start:
            self.send_error(416)
            return None
        stream = open(path, 'rb')
        stream.seek(start)
        self.remaining = end - start + 1
        self.send_response(206)
        self.send_header('Content-Type', self.guess_type(path))
        self.send_header('Content-Length', str(self.remaining))
        self.send_header('Content-Range', f'bytes {start}-{end}/{size}')
        self.end_headers()
        return stream

    def copyfile(self, source, output):
        if self.remaining is None:
            return super().copyfile(source, output)
        while self.remaining:
            data = source.read(min(1024 * 1024, self.remaining))
            if not data:
                break
            output.write(data)
            self.remaining -= len(data)

ThreadingHTTPServer(('127.0.0.1', a.port), EvidenceHandler).serve_forever()
