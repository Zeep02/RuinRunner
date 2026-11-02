"""Download explicitly free artist packs through the public anonymous itch flow.

Keep original archives and source pages outside Assets. No supplied code runs.
"""
from pathlib import Path
from urllib.request import build_opener, HTTPCookieProcessor, Request
from urllib.parse import urlencode
from http.cookiejar import CookieJar
from html.parser import HTMLParser
import re, json, hashlib, zipfile

ROOT = Path(__file__).resolve().parents[2] / 'ArtDirection/ExternalCandidates/20260921'
PACKS = [
    ('toko-knight', 'https://toko-vega.itch.io/32x32knight', ['32x32-pixelKnight.zip']),
    ('mana-seed', 'https://seliel-the-shaper.itch.io/character-base', ['FREE Mana Seed Character Base Demo 2.0.zip']),
    ('sunnyside', 'https://danieldiggle.itch.io/sunnyside',
     ['Sunnyside_World_ASSET_PACK_V2.1.zip','Sunnyside World - Human - 4,6,8 way movement - BETA']),
    ('pixel-poem-hero', 'https://pixel-poem.itch.io/dungeon-hero', ['RPG_Hero.zip']),
    ('craftpix-vampires', 'https://free-game-assets.itch.io/free-vampire-4-direction-pixel-character-sprite-pack',
     ['free-vampire-4-direction-pixel-character-sprite-pack.zip']),
    ('craftpix-enemies', 'https://free-game-assets.itch.io/free-enemy-pixel-pack-for-top-down-defense',
     ['Free-Enemy-Pixel-Pack-for-Top-Down-Defense.zip']),
    ('gamekrazzy-hero', 'https://gamekrazzy.itch.io/8-direction-top-down-character',
     ['TopDownCharacter.zip']),
    ('bumblebee-character', 'https://ordinary-bumblebee.itch.io/customizable-character-pack',
     ['32x32 Customizable Character Pack.zip']),
    ('iron-knight', 'https://hormelz.itch.io/8-directional-iron-knight',
     ['Knight-Only_BlueKnightUnarmed.zip']),
    ('craftpix-slimes', 'https://free-game-assets.itch.io/free-slime-mobs-pixel-art-top-down-sprite-pack',
     ['free-slime-mobs-pixel-art-top-down-sprite-pack.zip']),
    ('dotmancer-diamond', 'https://dotmancer.itch.io/diamond-top-down-pixel-art',
     ['full_version_with_godot_project_0.1.zip']),
]
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
