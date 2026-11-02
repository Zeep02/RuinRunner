"""Acquire only the author's explicitly free starter and body-only Warrior files.

Uses the public zero-price itch download flow, with no account or payment.
Archives and official terms stay outside Assets; no supplied code is executed.
"""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import re, json, hashlib, zipfile

root = Path(__file__).resolve().parents[2] / 'ArtDirection/ExternalCandidates/20260921/tiny-questers'
root.mkdir(parents=True, exist_ok=True)
opener = build_opener(HTTPCookieProcessor(CookieJar()))
opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
def get(url):
    with opener.open(url, timeout=30) as response:
        return response.read()
def post(url, html):
    csrf = re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"', html).group(1)
    with opener.open(Request(url, data=urlencode({'csrf_token': csrf}).encode()), timeout=30) as response:
        return json.load(response)
class Uploads(HTMLParser):
    def __init__(self):
        super().__init__(); self.files=[]; self.uid=None
    def handle_starttag(self, tag, attrs):
        attrs=dict(attrs)
        if 'data-upload_id' in attrs: self.uid=attrs['data-upload_id']
        if tag=='strong' and 'name' in attrs.get('class','').split() and attrs.get('title'):
            self.files.append((attrs['title'], self.uid)); self.uid=None

records=[]
for slug, expected in [('free-starter-pack','tiny-questers-free-starter-pack.zip'),
                       ('tiny-questers-warrior','tiny-questers-warrior-bodyonly-free.zip')]:
    base='https://bobddadoo.itch.io/'+slug
    html=get(base).decode(); (root/(slug+'-official.html')).write_text(html,encoding='utf-8')
    flow=json.loads('"'+re.search(r'"generate_download_url":"([^"]+)"',html).group(1)+'"')
    page=get(post(flow,html)['url']).decode(); parser=Uploads(); parser.feed(page)
    found=[uid for name,uid in parser.files if name==expected]
    assert len(found)==1 and found[0],parser.files
    archive=root/expected
    if archive.exists(): raise RuntimeError('Preserve existing original: '+str(archive))
    archive.write_bytes(get(post(base+'/file/'+found[0],page)['url']))
    destination=(root/'original'/slug).resolve()
    with zipfile.ZipFile(archive) as bundle:
        for entry in bundle.infolist():
            resolved=(destination/entry.filename).resolve()
            if not resolved.is_relative_to(destination): raise ValueError('Unsafe archive path')
        bundle.extractall(destination)
    records.append(dict(source=base,file=expected,bytes=archive.stat().st_size,
                        sha256=hashlib.sha256(archive.read_bytes()).hexdigest()))
(root/'acquisition.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps(records))
