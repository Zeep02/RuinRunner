"""Resolve preserved pre-atlas paths without changing historical manifests or captures."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PREVIOUS = 'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMaleCombat/'
ACTIVE = {'ArmedGripV3', 'WeaponStandingV4', 'WeaponReloadV2'}
ARCHIVED = {'Melee', 'ShortBlade', 'Thrust', 'Defense', 'RangedV7', 'WeaponStandingV1', 'WeaponStandingV2'}


def resolve_art_path(path):
    source = ROOT / path
    if source.is_file():
        return source
    normalized = str(path).replace('\\', '/')
    if normalized.startswith(PREVIOUS):
        version, suffix = normalized[len(PREVIOUS):].split('/', 1)
        if version in ACTIVE:
            source = ROOT / 'Assets/RuinRunner/ArtSources/Hero' / version / suffix
        elif version in ARCHIVED:
            source = ROOT / 'Assets/RuinRunner/ArtSources/Archive/SscaryMaleCombat' / version / suffix
    if not source.is_file():
        raise FileNotFoundError(f'Preserved art source is missing: {path}')
    return source
