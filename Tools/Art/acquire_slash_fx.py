"""Download only the two explicitly free Slash FX archives via the public itch flow."""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import re,json,hashlib

folder=Path(__file__).resolve().parents[2]/'ArtDirection/ExternalCandidates/20260921/slash-fx'
folder.mkdir(exist_ok=True)
base='https://jasontomlee.itch.io/slashfx'
opener=build_opener(HTTPCookieProcessor(CookieJar()));opener.addheaders=[('User-Agent','Mozilla/5.0')]
def get(url):
    with opener.open(url,timeout=25) as r:return r.read()
def post(url,html):
    token=re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"',html).group(1)
    with opener.open(Request(url,data=urlencode({'csrf_token':token}).encode()),timeout=25) as r:return json.load(r)
class Uploads(HTMLParser):
    def __init__(self):super().__init__();self.files=[];self.uid=None
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if 'data-upload_id' in a:self.uid=a['data-upload_id']
        if tag=='strong' and 'name' in a.get('class','').split() and a.get('title'):
            self.files.append((a['title'],self.uid));self.uid=None
html=get(base).decode();(folder/'official-page.html').write_text(html,encoding='utf-8')
flow=json.loads('"'+re.search(r'"generate_download_url":"([^"]+)"',html).group(1)+'"')
page=get(post(flow,html)['url']).decode();parser=Uploads();parser.feed(page)
records=[]
for expected in ['SlashFX FREE DEMO.rar','SlashFX Combos.rar']:
    matches=[uid for name,uid in parser.files if name==expected]
    assert len(matches)==1 and matches[0],parser.files
    archive=folder/expected
    archive.write_bytes(get(post(base+'/file/'+matches[0],page)['url']))
    records.append(dict(source=base,file=expected,bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest()))
(folder/'acquisition.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps(records))
