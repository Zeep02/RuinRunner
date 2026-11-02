"""Retain lossless originals; apply one constant gain per track (no recomposition)."""
from pathlib import Path
import hashlib, json, math, re, shutil, subprocess, wave
import numpy as np
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[2]
CANDIDATES = ROOT / 'ArtDirection/ExternalCandidates/20261001-music'
REVIEW = ROOT / 'ArtDirection/Review-20261001'
OUT = ROOT / 'Assets/RuinRunner/Resources/Audio/Campaign'
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
TRACKS = [('forest-whisper-theme', 'Forest Whisper', 1),
          ('exploration-theme', 'Exploration Theme', 2),
          ('ruined-city-theme', 'Ruined City Theme', 3)]

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    report = []
    for slug, title, stage in TRACKS:
        source = next((CANDIDATES / slug / 'original').rglob(title + '.wav'))
        analysis = subprocess.run([FFMPEG, '-hide_banner', '-i', str(source), '-af',
            'loudnorm=I=-20:TP=-3:LRA=11:print_format=json', '-f', 'null', '-'], capture_output=True, text=True, encoding='utf-8', check=True)
        loudness = json.loads(re.search(r'\{[^{}]*"input_i"[^{}]*\}', analysis.stderr).group())
        gain = min(-20 - float(loudness['input_i']), -3 - float(loudness['input_tp']))
        target = OUT / f'stage{stage}-music.wav'
        backup = REVIEW / 'before-music' / target.name
        if not backup.exists():
            shutil.copy2(target, backup)
            shutil.copy2(str(target) + '.meta', str(backup) + '.meta')
        subprocess.run([FFMPEG, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
            '-af', f'volume={gain:.8f}dB', '-c:a', 'pcm_s16le', str(target)], check=True)
        with wave.open(str(target)) as wav:
            channels, rate, frames = wav.getnchannels(), wav.getframerate(), wav.getnframes()
            data = np.frombuffer(wav.readframes(frames), dtype='<i2').reshape(-1,channels).astype(np.float32)/32768
        assert channels == 2 and rate == 44100
        assert float(np.max(np.abs(data))) < .72
        # Compare boundary jump to actual neighbouring sample changes; do not trim or fade the author's loop.
        jump = float(np.max(np.abs(data[0] - data[-1])))
        interior = float(np.percentile(np.abs(np.diff(data, axis=0)), 99.9))
        original_jump = jump
        declick_ms = 0
        if jump > .02 and jump > interior:
            declick_ms = 5
            n = round(rate * declick_ms / 1000)
            data[:n] *= np.linspace(0, 1, n)[:, None]
            data[-n:] *= np.linspace(1, 0, n)[:, None]
            with wave.open(str(target), 'wb') as wav:
                wav.setnchannels(channels); wav.setsampwidth(2); wav.setframerate(rate)
                wav.writeframes(np.rint(data*32768).astype('<i2').tobytes())
            jump = float(np.max(np.abs(data[0] - data[-1])))
        row = {'stage':stage, 'title':title, 'author':'Cleyton Kauffman', 'license':'CC0-1.0',
            'source':str(source.relative_to(ROOT)), 'source_sha256':sha(source),
            'output':str(target.relative_to(ROOT)), 'output_sha256':sha(target),
            'duration':frames/rate, 'sample_rate':rate, 'channels':channels,
            'original_integrated_lufs':float(loudness['input_i']), 'gain_db':gain,
            'estimated_integrated_lufs':float(loudness['input_i'])+gain,
            'peak':float(np.max(np.abs(data))), 'boundary_jump':jump, 'boundary_jump_before':original_jump,
            'edge_declick_ms':declick_ms, 'p999_sample_change':interior,
            'source_page':'https://opengameart.org/content/'+slug,
            'processing':'Constant gain plus 5 ms edge de-click only where measured discontinuity requires it; original loop length, pitch, tempo, stereo and arrangement preserved.'}
        report.append(row)
        print(json.dumps(row, ensure_ascii=False))
    (REVIEW / 'campaign-music-import.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

    # Replace pitched synthetic drones with the pack's original environment loops.
    ambience = ROOT / 'ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/Audio/Sounds/Ambient'
    ambient_report = []
    for stage, name in enumerate(['Wind.wav', 'WaveFar.wav', 'Wind2.wav'], 1):
        source, target = ambience / name, OUT / f'stage{stage}-ambience.wav'
        backup = REVIEW / 'before-music' / target.name
        if not backup.exists():
            shutil.copy2(target, backup)
            shutil.copy2(str(target)+'.meta', str(backup)+'.meta')
        shutil.copy2(source, target)
        with wave.open(str(source)) as wav:
            info = {'stage':stage, 'source':str(source.relative_to(ROOT)), 'output':str(target.relative_to(ROOT)),
                'duration':wav.getnframes()/wav.getframerate(), 'channels':wav.getnchannels(),
                'sample_rate':wav.getframerate(), 'source_sha256':sha(source), 'output_sha256':sha(target),
                'author':'Pixel-Boy and AAA', 'license':'CC0-1.0', 'processing':'None; exact source bytes.'}
        ambient_report.append(info)
        print(json.dumps(info, ensure_ascii=False))
    (REVIEW / 'campaign-ambience-import.json').write_text(json.dumps(ambient_report, ensure_ascii=False, indent=2), encoding='utf-8')

if __name__ == '__main__': main()
