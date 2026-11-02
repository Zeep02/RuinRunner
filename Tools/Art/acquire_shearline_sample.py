"""Acquire the explicitly labelled free eight-effect sample, not the paid pack."""
from pathlib import Path
from urllib.request import build_opener,HTTPCookieProcessor,Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
import re,json,zipfile,hashlib
base='https://godboyhappy.itch.io/shearline-pixel-melee-slash-vfx'
root=Path(__file__).resolve().parents[2]/'ArtDirection/ExternalCandidates/20260921/shearline';root.mkdir(exist_ok=True)
op=build_opener(HTTPCookieProcessor(CookieJar()));op.addheaders=[('User-Agent','Mozilla/5.0')]
page=op.open(base,timeout=25).read().decode();(root/'official-page.html').write_text(page,encoding='utf-8')
assert 'SHEARLINE free sampler - 8 effects, one per category' in page
uid=re.search(r'uploads for_demo.*?data-upload_id="(\d+)"',page).group(1)
token=re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"',page).group(1)
with op.open(Request(base+'/file/'+uid,data=urlencode({'csrf_token':token}).encode()),timeout=25) as r:response=json.load(r)
data=op.open(response['url'],timeout=25).read();archive=root/'free-sampler.zip';archive.write_bytes(data)
dest=root/'original';dest.mkdir(exist_ok=True)
with zipfile.ZipFile(archive) as z:
    for item in z.infolist():assert (dest/item.filename).resolve().is_relative_to(dest.resolve())
    z.extractall(dest)
record=dict(source=base,scope='Explicit free sampler only; no purchase. Candidate awaiting visual review.',bytes=len(data),sha256=hashlib.sha256(data).hexdigest())
(root/'acquisition.json').write_text(json.dumps(record,indent=2),encoding='utf-8');print(json.dumps(record))
