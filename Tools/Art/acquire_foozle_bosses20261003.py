"""Download explicitly free artist packs through the public anonymous itch flow.

Keep original archives and source pages outside Assets. No supplied code runs.
"""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import re, json, hashlib, zipfile

ROOT = Path(__file__).resolve().parents[2] / 'ArtDirection/ExternalCandidates/20261003'
PACKS = [('foozle-ancient','https://foozlecc.itch.io/lucifer-skeleton-ancient-boss',['Foozle_2DC0020_Lucifer_Skeleton_Ancient_Pixel_Art.zip']), ('foozle-king','https://foozlecc.itch.io/lucifer-skeleton-king-boss',['Foozle_2DC0021_Lucifer_Skeleton_King_Pixel_Art.zip'])]
opener = build_opener(HTTPCookieProcessor(CookieJar()))
opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
def get(url):
    with opener.open(url, timeout=60) as response: return response.read()
def post(url, html):
    csrf = re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"', html).group(1)
    with opener.open(Request(url, data=urlencode({'csrf_token':csrf}).encode()), timeout=60) as response:
        return json.load(response)
class Uploads(HTMLParser):
    def __init__(self): super().__init__(); self.files=[]; self.uid=None
    def handle_starttag(self, tag, attrs):
        attrs=dict(attrs)
        if 'data-upload_id' in attrs: self.uid=attrs['data-upload_id']
        if tag=='strong' and 'name' in attrs.get('class','').split() and attrs.get('title'):
            self.files.append((attrs['title'],self.uid)); self.uid=None

for name, base, allowed in PACKS:
    root=ROOT/name; root.mkdir(parents=True,exist_ok=True)
    if (root/'acquisition.json').exists():
        print(name+': original already acquired',flush=True); continue
    html=get(base).decode(); (root/'official.html').write_text(html,encoding='utf-8')
    flow=json.loads('"'+re.search(r'"generate_download_url":"([^"]+)"',html).group(1)+'"')
    page=get(post(flow,html)['url']).decode(); parser=Uploads(); parser.feed(page)
    records=[]
    for expected in allowed:
        found=[uid for filename,uid in parser.files if filename==expected]
        assert len(found)==1 and found[0],parser.files
        archive=root/expected
        if not archive.exists(): archive.write_bytes(get(post(base+'/file/'+found[0],page)['url']))
        destination=(root/'original').resolve()
        if zipfile.is_zipfile(archive):
            with zipfile.ZipFile(archive) as bundle:
                for entry in bundle.infolist():
                    if not (destination/entry.filename).resolve().is_relative_to(destination):
                        raise ValueError('Unsafe archive path')
                bundle.extractall(destination)
        elif archive.read_bytes()[4:6]==b'\xe0\xa5':
            destination.mkdir(parents=True,exist_ok=True)
            (destination/'human-eight-directions-beta.aseprite').write_bytes(archive.read_bytes())
        else: raise ValueError('Unrecognized original file format: '+expected)
        records.append(dict(source=base,file=expected,bytes=archive.stat().st_size,
                            sha256=hashlib.sha256(archive.read_bytes()).hexdigest()))
    if name=='craftpix-slimes':
        (root/'official-license.html').write_bytes(get('https://craftpix.net/file-licenses/'))
    (root/'acquisition.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    print(json.dumps(records),flush=True)
