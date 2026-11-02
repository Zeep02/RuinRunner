"""Fetch author-hosted public game-music submissions; preserve pages and archives."""
from pathlib import Path
import concurrent.futures, hashlib, json, re, urllib.request, urllib.parse, zipfile

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'ArtDirection/ExternalCandidates/20261001-music'
SLUGS = ['forest-whisper-theme', 'exploration-theme', 'ruined-city-theme']

def fetch(slug):
    target = DEST / slug
    target.mkdir(parents=True, exist_ok=True)
    url = 'https://opengameart.org/content/' + slug
    page = urllib.request.urlopen(url, timeout=45).read()
    (target / 'source-page.html').write_bytes(page)
    links = re.findall(r'href=["\']([^"\']+\.zip)["\']', page.decode('utf-8'))
    urls = list(dict.fromkeys(urllib.parse.urljoin(url, x) for x in links))
    if len(urls) != 1:
        raise RuntimeError((slug, urls))
    archive = target / urllib.parse.unquote(urls[0].rsplit('/', 1)[-1])
    if not archive.exists():
        with urllib.request.urlopen(urls[0], timeout=60) as response:
            archive.write_bytes(response.read())
    with zipfile.ZipFile(archive) as z:
        for entry in z.infolist():
            resolved = (target / 'original' / entry.filename).resolve()
            if not resolved.is_relative_to((target / 'original').resolve()):
                raise RuntimeError('Unsafe archive path')
        z.extractall(target / 'original')
    info = {'author':'Cleyton Kauffman', 'page':url, 'download':urls[0],
            'downloaded':'2026-10-01', 'page_license':'CC0 1.0 Universal',
            'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),
            'files':[str(x.relative_to(target)) for x in target.rglob('*') if x.is_file()]}
    (target / 'source.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
    return info

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(fetch, SLUGS):
            print(json.dumps(result, ensure_ascii=False))
