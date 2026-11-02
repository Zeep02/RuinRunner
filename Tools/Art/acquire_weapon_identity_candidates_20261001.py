"""Acquire only explicitly free official downloads for native effect review."""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import re,json,hashlib,concurrent.futures
root=Path(__file__).resolve().parents[2]
class Uploads(HTMLParser):
    def __init__(self):super().__init__();self.files=[];self.uid=None
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if 'data-upload_id' in a:self.uid=a['data-upload_id']
        if tag=='strong' and 'name' in a.get('class','').split() and a.get('title'):
            self.files.append((a['title'],self.uid));self.uid=None
def acquire(spec):
    slug,base,expected=spec
    folder=root/'ArtDirection/ExternalCandidates/20261001'/slug
    folder.mkdir(parents=True,exist_ok=True)
    opener=build_opener(HTTPCookieProcessor(CookieJar()));opener.addheaders=[('User-Agent','Mozilla/5.0')]
    def get(url):
        with opener.open(url,timeout=35) as r:return r.read()
    def post(url,html):
        token=re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"',html).group(1)
        with opener.open(Request(url,data=urlencode({'csrf_token':token}).encode()),timeout=35) as r:return json.load(r)
    html=get(base).decode();(folder/'official-page.html').write_text(html,encoding='utf-8')
    flow=json.loads('"'+re.search(r'"generate_download_url":"([^"]+)"',html).group(1)+'"')
    page=get(post(flow,html)['url']).decode();parser=Uploads();parser.feed(page)
    matches=[uid for name,uid in parser.files if name==expected]
    assert len(matches)==1 and matches[0],parser.files
    archive=folder/expected
    if not archive.exists():archive.write_bytes(get(post(base+'/file/'+matches[0],page)['url']))
    record=dict(source=base,file=expected,bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),scope='Free official archive for review; no paid files or purchase.')
    (folder/'acquisition.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
    return record
if __name__=='__main__':
    specs=[('frostwindz-slashes','https://frostwindz.itch.io/pixel-art-slashes','Pixel Art Animations - Slashes.zip'),
           ('icemaan-vfx','https://icemaan.itch.io/pixel-vfx-essentials','Pixel VFX Essentials.rar')]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        for r in pool.map(acquire,specs):print(json.dumps(r))
