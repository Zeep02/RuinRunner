"""PixelLab MCP transport; authentication stays in the process environment."""
import json
import os
import requests


class PixelLab:
    def __init__(self):
        self.session = requests.Session()
        self.headers = {
            'Authorization': 'Bearer ' + os.environ['PIXELLAB_API_TOKEN'],
            'Accept': 'application/json, text/event-stream',
        }
        self.sequence = 0
        self.rpc('initialize', {
            'protocolVersion': '2024-11-05', 'capabilities': {},
            'clientInfo': {'name': 'ruinrunner-art', 'version': '1.0'},
        })

    def rpc(self, method, params):
        self.sequence += 1
        response = self.session.post(
            'https://api.pixellab.ai/mcp', headers=self.headers,
            json={'jsonrpc': '2.0', 'id': self.sequence, 'method': method, 'params': params},
            timeout=90,
        )
        response.raise_for_status()
        if response.headers.get('Mcp-Session-Id'):
            self.headers['Mcp-Session-Id'] = response.headers['Mcp-Session-Id']
        if 'text/event-stream' in response.headers.get('Content-Type', ''):
            records = [json.loads(line[6:]) for line in response.text.splitlines() if line.startswith('data: ')]
            result = next(record for record in reversed(records) if record.get('id') == self.sequence)
        else:
            result = response.json()
        if 'error' in result:
            raise RuntimeError(json.dumps(result['error']))
        return result['result']

    def call(self, name, arguments):
        return self.rpc('tools/call', {'name': name, 'arguments': arguments})


def without_images(result):
    return {key: ([block for block in value if block.get('type') != 'image'] if key == 'content' else value)
            for key, value in result.items()}


if __name__ == '__main__':
    import sys
    name = sys.argv[1]
    arguments = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    print(json.dumps(without_images(PixelLab().call(name, arguments)), ensure_ascii=False, indent=2))
