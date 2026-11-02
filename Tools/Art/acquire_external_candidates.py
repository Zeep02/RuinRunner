"""Fetch explicitly free itch.io art archives through its public download flow.
No login, purchase, executable launch, or paid file endpoint is used.
"""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode, urljoin
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import json, re, hashlib, zipfile

ROOT = Path(__file__).resolve().parents[2] / 'ArtDirection/ExternalCandidates/20260921'
PACKS = [
    ('tiny-swords', 'https://pixelfrog-assets.itch.io/tiny-swords', 'Tiny Swords (Free Pack).zip'),
    ('cainos-basic', 'https://cainos.itch.io/pixel-art-top-down-basic', 'Pixel Art Top Down - Basic v1.2.3.zip'),
    ('zerie-tiny-rpg', 'https://zerie.itch.io/tiny-rpg-character-asset-pack', 'Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc.zip'),
    ('ninja-adventure', 'https://pixel-boy.itch.io/ninja-adventure-asset-pack', 'Ninja Adventure - Asset Pack.zip'),
]

class Uploads(HTMLParser):
    def __init__(self):
        super().__init__(); self.files = []; self.upload = None
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if 'data-upload_id' in a:
            self.upload = a['data-upload_id']
        if tag == 'strong' and 'name' in a.get('class', '').split() and a.get('title'):
            self.files.append((a['title'], self.upload)); self.upload = None

def csrf(page):
    token = re.search(r'<meta\s+name="csrf_token"\s+value="([^"]+)"', page)
    if not token: raise RuntimeError('No anonymous CSRF token')
    return token.group(1)

for key, page_url, requested in PACKS:
    folder = ROOT / key; folder.mkdir(parents=True, exist_ok=True)
    if (folder / 'acquisition.json').exists():
        print(key, 'already downloaded'); continue
    opener = build_opener(HTTPCookieProcessor(CookieJar()))
    opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
    def get(url):
        with opener.open(url, timeout=25) as response: return response.read()
    def post(url, page):
        with opener.open(Request(url, data=urlencode({'csrf_token': csrf(page)}).encode()), timeout=25) as response:
            return json.load(response)
    page = get(page_url).decode('utf-8')
    (folder / 'official-page.html').write_text(page, encoding='utf-8')
    match = re.search(r'"generate_download_url":"([^"]+)"', page)
    if not match: raise RuntimeError('No public free download flow: ' + key)
    flow = json.loads('"' + match.group(1) + '"')
    landing = post(flow, page)
    if 'url' not in landing: raise RuntimeError(str(landing))
    download_page = get(landing['url']).decode('utf-8')
    parser = Uploads(); parser.feed(download_page)
    matches = [(name, uid) for name, uid in parser.files if name == requested]
    if len(matches) != 1 or not matches[0][1]:
        raise RuntimeError('Expected free upload missing: ' + repr(parser.files))
    name, uid = matches[0]
    response = post(page_url + '/file/' + uid, download_page)
    if 'url' not in response: raise RuntimeError(str(response))
    archive = folder / name
    archive.write_bytes(get(response['url']))
    extracted = folder / 'original'; extracted.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        for member in z.infolist():
            target = (extracted / member.filename).resolve()
            if not target.is_relative_to(extracted.resolve()): raise RuntimeError('Unsafe archive path')
        z.extractall(extracted)
        names = z.namelist()
    record = dict(source=page_url, archive=name, bytes=archive.stat().st_size,
                  sha256=hashlib.sha256(archive.read_bytes()).hexdigest(), files=len(names),
                  scope='Official free download; candidate only. No runtime adoption or paid purchase.',
                  licenseSource='official-page.html; inspect packaged license before adoption')
    (folder / 'acquisition.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(record, ensure_ascii=False))
