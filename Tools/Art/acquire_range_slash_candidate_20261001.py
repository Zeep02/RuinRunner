"""Read-only native higher-density slash candidate; no runtime adoption."""
from acquire_weapon_identity_candidates_20261001 import acquire,root
import zipfile,json
record=acquire(('d3velopp-slashes','https://d3velopp.itch.io/slash-effects-pixel-2d-pack','SlashEffect_2d_pack.zip'))
folder=root/'ArtDirection/ExternalCandidates/20261001/d3velopp-slashes'
original=folder/'original';original.mkdir(exist_ok=True)
with zipfile.ZipFile(folder/record['file']) as archive:
    for member in archive.infolist():
        if not (original/member.filename).resolve().is_relative_to(original.resolve()):
            raise RuntimeError('Archive path escapes candidate directory')
    archive.extractall(original)
    record['files']=len(archive.infolist())
print(json.dumps(record))
