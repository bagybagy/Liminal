"""Prepare user-selected recordings, retaining measured beats rather than a BPM clock."""
import argparse
import hashlib
import json
import math
import subprocess
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parent.parent
CONFIG = ROOT / 'Tools/approved-stage-audio.json'
OUTPUT = ROOT / 'Assets/Liminal/Resources/StageAudio'
CACHE = ROOT / 'Verification/ApprovedStageMusic/analysis.json'


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def loudness(path):
    process = subprocess.run([
        'ffmpeg', '-hide_banner', '-nostats', '-i', str(path), '-af',
        'loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json', '-f', 'null', '-'
    ], capture_output=True, text=True, check=True)
    return json.JSONDecoder().raw_decode(process.stderr[process.stderr.rfind('{'):])[0]


def measured_harmony(chroma, frame_times, boundaries):
    templates, chords = [], []
    for root in range(12):
        for third in (3, 4):
            pitches = [root, (root + third) % 12, (root + 7) % 12]
            template = np.zeros(12)
            template[pitches] = [1, .85, .75]
            templates.append(template / np.linalg.norm(template))
            chords.append([48 + root, 48 + root + third, 48 + root + 7])
    templates = np.asarray(templates)
    result = []
    for start, end in zip(boundaries[:-1], boundaries[1:]):
        columns = (frame_times >= start) & (frame_times < end)
        energy = np.mean(chroma[:, columns], axis=1) if np.any(columns) else np.mean(chroma, axis=1)
        energy /= max(1e-9, np.linalg.norm(energy))
        scores = templates @ energy
        index = int(np.argmax(scores))
        result.append({'time': float(start), 'notes': chords[index],
                       'similarity': float(scores[index])})
    return result


def inspect(config):
    target = loudness(ROOT / 'Assets/Liminal/Audio/TidalMemory.wav')
    results = []
    for track in config['tracks']:
        source = ROOT / track['source']
        pcm, rate = sf.read(source, dtype='float32', always_2d=True)
        if not np.isfinite(pcm).all():
            raise ValueError(f'Nonfinite audio: {source}')
        mono = np.mean(pcm, axis=1)
        divisor = math.gcd(rate, 22050)
        mono = resample_poly(mono, 22050 // divisor, rate // divisor)
        hop = 128
        spectrum = librosa.stft(mono, n_fft=2048, hop_length=hop)
        harmonic, percussive = librosa.decompose.hpss(spectrum)
        envelope = librosa.onset.onset_strength(S=librosa.amplitude_to_db(np.abs(percussive)),
                                               sr=22050, hop_length=hop)
        tempo, beats = librosa.beat.beat_track(onset_envelope=envelope, sr=22050,
                                             hop_length=hop, start_bpm=124,
                                             trim=False, units='time')
        if len(beats) < 64:
            raise ValueError(f'Too few reliable beats: {source}')
        median_bpm = 60 / float(np.median(np.diff(beats)))
        if not 85 < median_bpm < 175:
            raise ValueError(f'Tempo needs manual half/double-time review: {median_bpm}, {source}')
        # Choose a four-beat phase from the strongest recurring onset pattern.
        beat_frames = np.minimum(np.rint(beats * 22050 / hop).astype(int), len(envelope) - 1)
        phase_energy = [float(np.mean(envelope[beat_frames[phase::4]])) for phase in range(4)]
        phase = int(np.argmax(phase_energy))
        start_index = phase
        usable_beats = (len(beats) - 1 - start_index) // 4 * 4
        start = float(beats[start_index])
        end = float(beats[start_index + usable_beats])
        selected = beats[start_index:start_index + usable_beats + 1]
        chroma = librosa.feature.chroma_stft(S=np.abs(harmonic) ** 2, sr=22050,
                                            n_fft=2048, hop_length=hop)
        frame_times = librosa.frames_to_time(np.arange(chroma.shape[1]), sr=22050, hop_length=hop)
        harmony = measured_harmony(chroma, frame_times, selected[::4])
        meter = loudness(source)
        target_db = float(target['input_i'])
        gain_db = min(target_db - float(meter['input_i']), -1.5 - float(meter['input_tp']))
        record = {
            **track, 'source_sha256': digest(source), 'source_rate': rate,
            'source_seconds': len(pcm) / rate, 'source_peak': float(np.max(np.abs(pcm))),
            'estimated_bpm': float(np.asarray(tempo).flat[0]), 'median_beat_bpm': median_bpm,
            'beat_interval_p10_p90': np.quantile(np.diff(selected), [.1, .9]).tolist(),
            'beat_times': selected.tolist(), 'crop_start': start, 'crop_end': end,
            'bars': usable_beats // 4, 'estimated_downbeat_phase': phase,
            'downbeat_method': 'four-beat recurring onset energy; estimated, not a human-verified score',
            'harmony': harmony, 'harmony_method': 'HPSS harmonic STFT chroma / major-minor triad similarity; estimated',
            'input_lufs': float(meter['input_i']), 'tidal_memory_lufs': target_db, 'gain_db': gain_db,
            'input_true_peak_db': float(meter['input_tp'])
        }
        results.append(record)
        print(json.dumps({key: record[key] for key in ['name', 'median_beat_bpm', 'crop_start',
              'crop_end', 'bars', 'input_lufs', 'tidal_memory_lufs', 'gain_db']}, indent=2), flush=True)
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    CACHE.write_text(json.dumps({'tracks': results}, indent=2), encoding='utf-8')


def prepare(config):
    records = json.loads(CACHE.read_text(encoding='utf-8'))['tracks']
    OUTPUT.mkdir(parents=True, exist_ok=True)
    rate = config['sample_rate']
    manifest = {'approved_by': 'tete / direct user selection 2026-10-04',
                'main_and_whale': 'Assets/Liminal/Audio/TidalMemory.wav',
                'timing': 'Measured sample marks; no tempo stretch or pitch shift', 'tracks': []}
    for record in records:
        source = ROOT / record['source']
        if digest(source) != record['source_sha256']:
            raise ValueError(f'Source changed after analysis: {source}')
        pcm, original_rate = sf.read(source, dtype='float32', always_2d=True)
        divisor = math.gcd(rate, original_rate)
        pcm = resample_poly(pcm, rate // divisor, original_rate // divisor, axis=0)
        start, end = round(record['crop_start'] * rate), round(record['crop_end'] * rate)
        pcm = pcm[start:end].copy()
        pcm *= 10 ** (record['gain_db'] / 20)
        # Five-millisecond edge de-click; no remix, EQ, compression or tempo changes.
        edge = round(.005 * rate)
        ramp = np.linspace(0, 1, edge, dtype=np.float32)[:, None]
        pcm[:edge] *= ramp
        pcm[-edge:] *= ramp[::-1]
        if np.max(np.abs(pcm)) >= .999:
            raise ValueError(f'Clipping after gain alignment: {record["name"]}')
        destination = OUTPUT / (record['name'] + '.wav')
        sf.write(destination, pcm, rate, subtype='PCM_16')
        marks = [round(time * rate) - start for time in record['beat_times']]
        if marks[0] != 0 or marks[-1] != len(pcm):
            raise ValueError('Trim and measured score disagree')
        beats = marks[:-1]
        eighths = [value for a, b in zip(marks[:-1], marks[1:]) for value in (a, round((a + b) / 2))]
        harmony = [{'sample': max(0, round(chord['time'] * rate) - start), 'notes': chord['notes']}
                   for chord in record['harmony']]
        timeline = {
            'themeId': record['theme_id'], 'theme': record['name'],
            'sampleRate': rate, 'sampleCount': len(pcm), 'bars': record['bars'],
            'bpm': round(record['median_beat_bpm']), 'tempoBpm': record['median_beat_bpm'],
            'sourceSha256': digest(destination), 'beats': beats, 'eighths': eighths,
            'sections': [marks[i] for i in range(0, len(beats), 16)], 'harmony': harmony,
            'timingMethod': record['downbeat_method'], 'harmonyMethod': record['harmony_method']
        }
        (OUTPUT / (record['name'] + 'Timeline.json')).write_text(json.dumps(timeline), encoding='utf-8')
        generation = json.loads((ROOT / record['generation_record']).read_text(encoding='utf-8'))
        manifest['tracks'].append({**{key: value for key, value in record.items() if key not in ('beat_times', 'harmony')},
            'asset': destination.relative_to(ROOT).as_posix(), 'processed_sha256': timeline['sourceSha256'],
            'processed_seconds': len(pcm) / rate, 'peak': float(np.max(np.abs(pcm))),
            'model': generation['model'], 'language_model': generation['lm'],
            'generation_task': generation['params']['task_type'],
            'generation_seed': generation['params']['seed'],
            'reference_audio': generation['params']['reference_audio'],
            'source_audio': generation['params']['src_audio']})
    (OUTPUT / 'ApprovedTracks.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps({'prepared': [track['asset'] for track in manifest['tracks']]}, indent=2))


if __name__ == '__main__':
    arguments = argparse.ArgumentParser()
    arguments.add_argument('--prepare', action='store_true', help='Use the saved analysis, without re-running audio analysis')
    args = arguments.parse_args()
    configuration = json.loads(CONFIG.read_text(encoding='utf-8'))
    prepare(configuration) if args.prepare else inspect(configuration)
