"""Adopt approved full-length ending songs with measured, approximate score maps."""
import argparse
import hashlib
import json
import math
import re
import subprocess
import uuid
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

ROOT = Path(__file__).resolve().parent.parent
CONFIG = ROOT / "Tools/approved-ending-audio.json"
OUTPUT = ROOT / "Assets/Liminal/Resources/StageAudio"
RATE = 22050
HOP = 128
TIMING_METHOD = (
    "librosa HPSS/onset beat tracking; beats are sample-zero start boundary followed by every "
    "detected beat unchanged. EOF is an auxiliary endpoint only. A partial final bar is retained; "
    "bars is ceil(beat count / 4). The inferred start and downbeat/beat analysis are approximate."
)
HARMONY_METHOD = (
    "HPSS harmonic STFT chroma with major/minor triad similarity; estimated, not human-verified"
)
HARMONY_RETIMING_METHOD = (
    "Existing estimated chords retained and assigned to the nearest previous chord at each exact "
    "four-beat window start; final window ends at EOF. No new chroma analysis was run."
)


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def ffmpeg_measure(path, sample_rate=None):
    filters = []
    if sample_rate:
        filters.append(f"aresample={sample_rate}")
    filters.append("loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json")
    process = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", ",".join(filters), "-f", "null", "-"],
        capture_output=True, text=True, check=True,
    )
    start = process.stderr.rfind("{")
    if start < 0:
        raise RuntimeError(f"FFmpeg loudness measurement missing for {path}")
    return json.JSONDecoder().raw_decode(process.stderr[start:])[0]


def measured_harmony(chroma, frame_times, boundaries):
    templates, chords = [], []
    for root in range(12):
        for third in (3, 4):
            pitches = [root, (root + third) % 12, (root + 7) % 12]
            template = np.zeros(12)
            template[pitches] = [1, 0.85, 0.75]
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
        result.append({"time": float(start), "notes": chords[index], "similarity": float(scores[index])})
    return result


def generation_data(track):
    path = ROOT / track["generation_record"]
    record = json.loads(path.read_text(encoding="utf-8"))
    params = record["params"]
    if record.get("task_type") != "text2music" or params.get("task_type") != "text2music":
        raise ValueError(f"Expected original text2music generation: {path}")
    if params.get("reference_audio") is not None or params.get("src_audio") is not None:
        raise ValueError(f"Unexpected audio reference/source in generation record: {path}")
    if int(params.get("bpm", -1)) != track["declared_bpm"]:
        raise ValueError(f"Declared BPM mismatch: {path}")
    return record


def analyze_track(track):
    source = ROOT / track["source"]
    generation_path = ROOT / track["generation_record"]
    if not source.is_file() or not generation_path.is_file():
        raise FileNotFoundError(source if not source.is_file() else generation_path)
    generation = generation_data(track)
    pcm, source_rate = sf.read(source, dtype="float32", always_2d=True)
    seconds = len(pcm) / source_rate
    if source_rate != 48000 or pcm.shape[1] != 2 or abs(seconds - track["expected_seconds"]) > 1 / source_rate:
        raise ValueError(f"Unexpected source audio format/duration: {source}")
    if not np.isfinite(pcm).all():
        raise ValueError(f"Non-finite source samples: {source}")

    mono = np.mean(pcm, axis=1)
    divisor = math.gcd(source_rate, RATE)
    mono = resample_poly(mono, RATE // divisor, source_rate // divisor)
    spectrum = librosa.stft(mono, n_fft=2048, hop_length=HOP)
    harmonic, percussive = librosa.decompose.hpss(spectrum)
    envelope = librosa.onset.onset_strength(
        S=librosa.amplitude_to_db(np.abs(percussive)), sr=RATE, hop_length=HOP
    )
    tempo, detected = librosa.beat.beat_track(
        onset_envelope=envelope, sr=RATE, hop_length=HOP,
        start_bpm=track["declared_bpm"], trim=False, units="time",
    )
    detected = np.asarray(detected, dtype=float).reshape(-1)
    detected = detected[np.isfinite(detected) & (detected > 0) & (detected < seconds)]
    detected = np.unique(detected)
    if len(detected) < 32:
        raise ValueError(f"Too few measured beat anchors for a reliable timeline: {source}")
    intervals = np.diff(detected)
    median_bpm = 60.0 / float(np.median(intervals))
    if not math.isfinite(median_bpm) or median_bpm <= 0:
        raise ValueError(f"Invalid measured beat interval: {source}")

    beat_frames = np.minimum(np.rint(detected * RATE / HOP).astype(int), len(envelope) - 1)
    phase_energy = [float(np.mean(envelope[beat_frames[phase::4]])) for phase in range(4)]
    phase = int(np.argmax(phase_energy))

    beat_times = np.concatenate(([0.0], detected))
    beat_count = len(beat_times)
    bars = max(1, (beat_count + 3) // 4)
    boundary_times = np.concatenate((beat_times, [seconds]))
    if np.any(np.diff(boundary_times) <= 0):
        raise ValueError(f"Could not form a monotone full-duration beat map: {source}")

    chroma = librosa.feature.chroma_stft(
        S=np.abs(harmonic) ** 2, sr=RATE, n_fft=2048, hop_length=HOP
    )
    frame_times = librosa.frames_to_time(np.arange(chroma.shape[1]), sr=RATE, hop_length=HOP)
    bar_boundaries = np.concatenate((boundary_times[np.arange(0, beat_count, 4)], [seconds]))
    harmony = measured_harmony(chroma, frame_times, bar_boundaries)
    if not harmony or harmony[0]["time"] != 0:
        raise ValueError(f"Harmony analysis must begin at source time zero: {source}")

    source_info = sf.info(source)
    meter = ffmpeg_measure(source, track["sample_rate"])
    return {
        "track": track, "source_sha256": digest(source),
        "source_rate": source_rate, "source_channels": pcm.shape[1],
        "source_frames": source_info.frames, "source_seconds": seconds,
        "source_peak": float(np.max(np.abs(pcm))),
        "declared_bpm": track["declared_bpm"],
        "librosa_tempo_bpm": float(np.asarray(tempo).reshape(-1)[0]),
        "median_beat_bpm": median_bpm,
        "beat_interval_p10_p50_p90_seconds": np.quantile(intervals, [0.1, 0.5, 0.9]).tolist(),
        "measured_beat_times_seconds": detected.tolist(),
        "measured_beat_count": len(detected),
        "estimated_downbeat_phase": phase, "phase_energy": phase_energy,
        "bars": bars, "timeline_beat_count": beat_count,
        "timeline_beat_times_seconds": beat_times.tolist(),
        "grid_boundary_times_seconds": boundary_times.tolist(), "harmony": harmony,
        "input_lufs": float(meter["input_i"]),
        "input_true_peak_dbtp": float(meter["input_tp"]),
        "generation": generation,
    }


def guid_for(path):
    if path.is_file():
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"), re.MULTILINE)
        if match:
            return match.group(1)
    return uuid.uuid4().hex


def write_meta(asset, audio):
    meta = asset.with_name(asset.name + ".meta")
    guid = guid_for(meta)
    if audio:
        lines = [
            "fileFormatVersion: 2", f"guid: {guid}", "AudioImporter:",
            "  externalObjects: {}", "  serializedVersion: 8", "  defaultSettings:",
            "    serializedVersion: 2", "    loadType: 0", "    sampleRateSetting: 2",
            "    sampleRateOverride: 44100", "    compressionFormat: 1", "    quality: 0.9",
            "    conversionMode: 0", "    preloadAudioData: 1", "  platformSettingOverrides: {}",
            "  forceToMono: 0", "  normalize: 0", "  loadInBackground: 0",
            "  ambisonic: 0", "  3D: 1", "  userData: ",
            "  assetBundleName: ", "  assetBundleVariant: ", "",
        ]
    else:
        lines = [
            "fileFormatVersion: 2", f"guid: {guid}", "TextScriptImporter:",
            "  externalObjects: {}", "  userData: ", "  assetBundleName: ",
            "  assetBundleVariant: ", "",
        ]
    meta.write_text("\n".join(lines), encoding="utf-8")


def save_json(path, payload):
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def make_timeline(analysis, sample_count, processed_hash, rate):
    count = sample_count
    grid = np.rint(np.asarray(analysis["grid_boundary_times_seconds"]) * rate).astype(np.int64)
    if len(grid) != analysis["timeline_beat_count"] + 1:
        raise ValueError(f"Beat boundary count changed: {analysis['track']['name']}")
    grid[0], grid[-1] = 0, count
    if np.any(np.diff(grid) <= 0):
        raise ValueError(f"Sample-quantized beat grid is not strictly increasing: {analysis['track']['name']}")
    beats = grid[:-1]
    eighths = np.asarray(
        [value for start, end in zip(grid[:-1], grid[1:]) for value in (start, round((start + end) / 2))],
        dtype=np.int64,
    )
    sections = grid[np.arange(0, len(beats), 16)]
    harmony = [
        {"sample": min(count - 1, max(0, round(chord["time"] * rate))), "notes": chord["notes"]}
        for chord in analysis["harmony"]
    ]
    return {
        "themeId": analysis["track"]["theme_id"], "theme": analysis["track"]["name"],
        "sampleRate": rate, "sampleCount": count, "bars": analysis["bars"],
        "bpm": round(analysis["median_beat_bpm"]), "tempoBpm": analysis["median_beat_bpm"],
        "sourceSha256": processed_hash, "beats": beats.tolist(), "eighths": eighths.tolist(),
        "sections": sections.tolist(), "harmony": harmony,
        "timingMethod": TIMING_METHOD, "timingApproximate": True,
        "timelineBeatCount": analysis["timeline_beat_count"],
        "startBoundaryInferred": True,
        "partialFinalBarBeats": analysis["timeline_beat_count"] % 4,
        "fullDurationBoundarySeconds": [0.0, count / rate],
        "harmonyMethod": analysis.get("harmony_method", HARMONY_METHOD),
    }


def prepare_track(analysis, target_lufs, target_tp):
    track = analysis["track"]
    source = ROOT / track["source"]
    if digest(source) != analysis["source_sha256"]:
        raise ValueError(f"Source changed after analysis: {source}")
    pcm, source_rate = sf.read(source, dtype="float32", always_2d=True)
    divisor = math.gcd(track["sample_rate"], source_rate)
    pcm = resample_poly(pcm, track["sample_rate"] // divisor, source_rate // divisor, axis=0)
    expected_frames = round(analysis["source_seconds"] * track["sample_rate"])
    if abs(len(pcm) - expected_frames) > 1 or not np.isfinite(pcm).all():
        raise ValueError(f"Resampling changed duration or produced invalid samples: {source}")

    gain_db = min(target_lufs - analysis["input_lufs"], target_tp - analysis["input_true_peak_dbtp"])
    pcm *= np.float32(10 ** (gain_db / 20))
    peak = float(np.max(np.abs(pcm)))
    if peak >= 1:
        raise ValueError(f"Gain-only leveling would clip {track['name']}; no limiter will be applied")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    destination = OUTPUT / (track["name"] + ".wav")
    sf.write(destination, pcm, track["sample_rate"], subtype="PCM_16", format="WAV")
    output_meter = ffmpeg_measure(destination)
    actual_tp = float(output_meter["input_tp"])
    if actual_tp > target_tp:
        correction_db = target_tp - actual_tp - 0.05
        pcm *= np.float32(10 ** (correction_db / 20))
        gain_db += correction_db
        peak = float(np.max(np.abs(pcm)))
        if peak >= 1:
            raise ValueError(f"True-peak correction would clip {track['name']}")
        sf.write(destination, pcm, track["sample_rate"], subtype="PCM_16", format="WAV")
        output_meter = ffmpeg_measure(destination)
        actual_tp = float(output_meter["input_tp"])
        if actual_tp > target_tp:
            raise ValueError(f"True peak exceeds {target_tp} dBTP after gain-only correction: {track['name']}")

    info = sf.info(destination)
    if (info.format, info.subtype, info.samplerate, info.channels) != ("WAV", "PCM_16", track["sample_rate"], 2):
        raise ValueError(f"Unexpected processed format: {destination}")
    if abs(info.frames / info.samplerate - analysis["source_seconds"]) > 1 / info.samplerate:
        raise ValueError(f"Processed audio does not retain full source duration: {destination}")
    if not np.isfinite(pcm).all() or np.max(np.abs(pcm)) >= 1:
        raise ValueError(f"Non-finite or clipped processed samples: {destination}")
    return {
        "destination": destination, "processed_sha256": digest(destination),
        "processed_seconds": info.frames / info.samplerate, "sample_count": info.frames,
        "peak": peak, "gain_db": gain_db,
        "output_lufs": float(output_meter["input_i"]), "output_true_peak_dbtp": actual_tp,
        "input_lufs": analysis["input_lufs"],
        "input_true_peak_dbtp": analysis["input_true_peak_dbtp"],
    }


def track_manifest(analysis, prepared, target_lufs, target_tp):
    track = analysis["track"]
    generation = analysis["generation"]
    params = generation["params"]
    job = generation.get("queue_job", {})
    timeline_path = OUTPUT / (track["name"] + "Timeline.json")
    return {
        "theme_id": track["theme_id"], "name": track["name"], "stage": track["stage"],
        "source": track["source"], "generation_record": track["generation_record"],
        "source_sha256": analysis["source_sha256"],
        "source_rate": analysis["source_rate"], "source_channels": analysis["source_channels"],
        "source_seconds": analysis["source_seconds"], "source_peak": analysis["source_peak"],
        "model": generation["model"], "language_model": generation["lm"],
        "generation_task": params["task_type"], "generation_seed": params.get("seed"),
        "declared_bpm": analysis["declared_bpm"], "generation_keyscale": params.get("keyscale"),
        "generation_prompt": params.get("caption", ""), "lyrics_file": generation.get("lyrics_file"),
        "reference_url": job.get("reference_url"), "reference_title": job.get("reference_title"),
        "reference_status": job.get("reference_status"),
        "reference_audio": params.get("reference_audio"), "source_audio": params.get("src_audio"),
        "asset": prepared["destination"].relative_to(ROOT).as_posix(),
        "processed_sha256": prepared["processed_sha256"],
        "processed_seconds": prepared["processed_seconds"], "peak": prepared["peak"],
        "input_lufs_after_resample": prepared["input_lufs"], "target_lufs": target_lufs,
        "gain_db": prepared["gain_db"], "output_lufs": prepared["output_lufs"],
        "input_true_peak_dbtp": prepared["input_true_peak_dbtp"],
        "output_true_peak_dbtp": prepared["output_true_peak_dbtp"],
        "true_peak_ceiling_dbtp": target_tp,
        "processing": [
            "polyphase resample to 44100 Hz stereo",
            "gain-only loudness alignment; no limiter, compression, EQ, fade, crop, tempo stretch, or pitch shift",
            "PCM 16-bit WAV",
        ],
        "tempo_analysis": {
            "method": "librosa HPSS percussive onset envelope and dynamic-programming beat tracker",
            "tracker_start_bpm_prior_only": analysis["declared_bpm"],
            "librosa_tempo_bpm": analysis["librosa_tempo_bpm"],
            "median_measured_beat_bpm": analysis["median_beat_bpm"],
            "beat_interval_p10_p50_p90_seconds": analysis["beat_interval_p10_p50_p90_seconds"],
            "measured_beat_count": analysis["measured_beat_count"],
            "measured_beat_times_seconds": analysis["measured_beat_times_seconds"],
            "estimated_downbeat_phase": analysis["estimated_downbeat_phase"],
            "downbeat_phase_energy": analysis["phase_energy"],
            "timeline_beat_count": analysis["timeline_beat_count"],
            "timeline_beat_times_seconds": analysis["timeline_beat_times_seconds"],
            "bars": analysis["bars"],
            "partial_final_bar_beats": analysis["timeline_beat_count"] % 4,
            "start_boundary_inferred": True,
            "full_duration_boundary_seconds": [0.0, analysis["source_seconds"]],
            "approximate": True, "approximation": TIMING_METHOD,
            "human_listening_verified": False,
        },
        "harmony_method": HARMONY_METHOD,
        "harmony_retiming_method": HARMONY_RETIMING_METHOD,
        "timeline": timeline_path.relative_to(ROOT).as_posix(),
        "timeline_sha256": digest(timeline_path),
    }


def verify_track(track, manifest_track, target_tp=None):
    source = ROOT / track["source"]
    wav = ROOT / manifest_track["asset"]
    timeline_path = ROOT / manifest_track["timeline"]
    if digest(source) != manifest_track["source_sha256"] or digest(wav) != manifest_track["processed_sha256"]:
        raise ValueError(f"Source/output SHA-256 mismatch: {track['name']}")
    info = sf.info(wav)
    if (info.format, info.subtype, info.samplerate, info.channels) != ("WAV", "PCM_16", 44100, 2):
        raise ValueError(f"Unexpected WAV format: {wav}")
    if abs(info.frames / info.samplerate - track["expected_seconds"]) > 1 / info.samplerate:
        raise ValueError(f"Full-length check failed: {wav}")
    if target_tp is not None:
        audio, _ = sf.read(wav, dtype="float32", always_2d=True)
        if not np.isfinite(audio).all() or np.max(np.abs(audio)) >= 1:
            raise ValueError(f"Non-finite or clipped samples: {wav}")
        sample_peak = float(np.max(np.abs(audio)))
    else:
        sample_peak = manifest_track["peak"]
    timeline = json.loads(timeline_path.read_text(encoding="utf-8"))
    count = info.frames
    beats, eighths = timeline["beats"], timeline["eighths"]
    sections, harmony = timeline["sections"], timeline["harmony"]
    if timeline["sampleCount"] != count or timeline["sourceSha256"] != digest(wav):
        raise ValueError(f"Timeline bounds/hash mismatch: {timeline_path}")
    if digest(timeline_path) != manifest_track.get("timeline_sha256"):
        raise ValueError(f"Timeline metadata SHA-256 mismatch: {timeline_path}")
    if timeline["themeId"] != track["theme_id"] or timeline["theme"] != track["name"]:
        raise ValueError(f"Timeline identity mismatch: {timeline_path}")
    if timeline["bars"] != (len(beats) + 3) // 4 or len(eighths) != len(beats) * 2:
        raise ValueError(f"Timeline cardinality mismatch: {timeline_path}")
    if (timeline.get("timelineBeatCount") != len(beats) or not timeline.get("startBoundaryInferred") or
            timeline.get("partialFinalBarBeats") != len(beats) % 4 or
            timeline.get("fullDurationBoundarySeconds") != [0.0, count / timeline["sampleRate"]]):
        raise ValueError(f"Ending boundary metadata mismatch: {timeline_path}")
    analysis = manifest_track["tempo_analysis"]
    expected_beats = [round(time * timeline["sampleRate"])
                      for time in analysis["timeline_beat_times_seconds"]]
    if expected_beats != beats:
        raise ValueError(f"Measured beat anchors were changed: {timeline_path}")
    for marks in (beats, eighths, sections):
        if not marks or marks[0] != 0 or any(a >= b for a, b in zip(marks, marks[1:])) or marks[-1] >= count:
            raise ValueError(f"Timeline sample bounds/order mismatch: {timeline_path}")
    expected_sections = beats[::16]
    if sections != expected_sections:
        raise ValueError(f"Section markers do not follow every 16 preserved beats: {timeline_path}")
    for index, beat in enumerate(beats):
        end = beats[index + 1] if index + 1 < len(beats) else count
        if eighths[index * 2] != beat or eighths[index * 2 + 1] != round((beat + end) / 2):
            raise ValueError(f"Eighth markers do not use the next beat/EOF boundary: {timeline_path}")
    if not harmony or harmony[0]["sample"] != 0 or any(
        a["sample"] >= b["sample"] for a, b in zip(harmony, harmony[1:])
    ) or harmony[-1]["sample"] >= count or len(harmony) != timeline["bars"]:
        raise ValueError(f"Harmony bounds/order mismatch: {timeline_path}")
    if target_tp is not None:
        meter = ffmpeg_measure(wav)
        output_tp = float(meter["input_tp"])
        if output_tp > target_tp:
            raise ValueError(f"True peak exceeds {target_tp} dBTP: {wav}")
        output_lufs = float(meter["input_i"])
    else:
        output_tp = manifest_track["output_true_peak_dbtp"]
        output_lufs = manifest_track["output_lufs"]
    return {
        "name": track["name"], "seconds": info.frames / info.samplerate,
        "sha256": digest(wav), "sample_peak": sample_peak,
        "lufs": output_lufs, "true_peak_dbtp": output_tp,
        "timeline_beats": len(beats), "timeline_bars": timeline["bars"],
    }


def prepare(config):
    target_path = ROOT / config["target_audio"]
    if not target_path.is_file():
        raise FileNotFoundError(target_path)
    target_lufs = float(ffmpeg_measure(target_path)["input_i"])
    target_tp = float(config["true_peak_ceiling_dbtp"])
    analyses = [
        analyze_track({**track, "sample_rate": config["sample_rate"]})
        for track in config["tracks"]
    ]
    if [item["track"]["theme_id"] for item in analyses] != [5, 6, 7, 8]:
        raise ValueError("Ending tracks must occupy theme ids 5..8 in the approved order")

    manifest_tracks = []
    for analysis in analyses:
        prepared = prepare_track(analysis, target_lufs, target_tp)
        timeline = make_timeline(
            analysis, prepared["sample_count"], prepared["processed_sha256"], config["sample_rate"]
        )
        timeline_path = OUTPUT / (analysis["track"]["name"] + "Timeline.json")
        save_json(timeline_path, timeline)
        write_meta(timeline_path, audio=False)
        write_meta(prepared["destination"], audio=True)
        manifest_tracks.append(track_manifest(analysis, prepared, target_lufs, target_tp))

    manifest = {
        "approved_by": config["approved_by"], "approval_date": config["approval_date"],
        "license_basis": config["license_basis"],
        "playback": "Play each full source from beginning to end once; no looping or trimming.",
        "target_audio": config["target_audio"], "target_lufs": target_lufs,
        "audio_format": {
            "sample_rate": config["sample_rate"], "channels": 2, "encoding": "WAV PCM 16-bit",
            "unity_vorbis_quality": 0.9, "unity_load_type": 0,
            "unity_sample_rate_override": 44100, "unity_normalize": False,
        },
        "gain_policy": "Gain only to TidalMemory integrated LUFS subject to the true-peak ceiling; no limiter.",
        "timing_policy": TIMING_METHOD,
        "tracks": manifest_tracks,
    }
    manifest_path = OUTPUT / "EndingTracks.json"
    save_json(manifest_path, manifest)
    write_meta(manifest_path, audio=False)

    results = [
        verify_track(track, result, target_tp)
        for track, result in zip(config["tracks"], manifest_tracks)
    ]
    print(json.dumps({
        "prepared": results, "target_lufs": target_lufs,
        "timeline": manifest_path.relative_to(ROOT).as_posix(),
    }, indent=2))


def rebuild_maps(config):
    manifest_path = OUTPUT / "EndingTracks.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if [item["theme_id"] for item in manifest["tracks"]] != [5, 6, 7, 8]:
        raise ValueError("Ending manifest is not in the approved theme order")

    for track, item in zip(config["tracks"], manifest["tracks"]):
        wav = ROOT / item["asset"]
        timeline_path = ROOT / item["timeline"]
        old_timeline = json.loads(timeline_path.read_text(encoding="utf-8"))
        info = sf.info(wav)
        rate = info.samplerate
        if (rate, info.channels, info.subtype) != (config["sample_rate"], 2, "PCM_16"):
            raise ValueError(f"Unexpected existing adopted audio format: {wav}")
        if digest(wav) != item["processed_sha256"]:
            raise ValueError(f"Refusing to rebuild against a changed WAV: {wav}")

        timing = item["tempo_analysis"]
        detected = timing["measured_beat_times_seconds"]
        if len(detected) != timing["measured_beat_count"]:
            raise ValueError(f"Saved measured beat provenance is incomplete: {track['name']}")
        duration = info.frames / rate
        beat_times = [0.0, *detected]
        boundary_times = np.asarray([*beat_times, duration], dtype=float)
        samples = np.rint(boundary_times * rate).astype(np.int64)
        samples[0], samples[-1] = 0, info.frames
        if np.any(np.diff(boundary_times) <= 0) or np.any(np.diff(samples) <= 0):
            raise ValueError(f"Saved measured beats do not fit the full source: {track['name']}")

        beat_count = len(beat_times)
        bars = (beat_count + 3) // 4
        old_harmony = old_timeline["harmony"]
        old_harmony_samples = np.asarray([chord["sample"] for chord in old_harmony], dtype=np.int64)
        harmony = []
        for index in range(0, beat_count, 4):
            sample = int(samples[index])
            nearest = int(np.argmin(np.abs(old_harmony_samples - sample)))
            chord = old_harmony[nearest]
            harmony.append({
                "time": sample / rate,
                "notes": chord["notes"],
                "similarity": chord.get("similarity"),
            })

        analysis = {
            "track": {"theme_id": track["theme_id"], "name": track["name"]},
            "grid_boundary_times_seconds": boundary_times.tolist(),
            "timeline_beat_count": beat_count, "bars": bars,
            "median_beat_bpm": timing["median_measured_beat_bpm"],
            "harmony": harmony, "harmony_method": HARMONY_METHOD + "; " + HARMONY_RETIMING_METHOD,
        }
        timeline = make_timeline(analysis, info.frames, item["processed_sha256"], rate)
        save_json(timeline_path, timeline)

        timing["timeline_beat_times_seconds"] = beat_times
        timing["timeline_beat_count"] = beat_count
        timing["bars"] = bars
        timing["partial_final_bar_beats"] = beat_count % 4
        timing["start_boundary_inferred"] = True
        timing["full_duration_boundary_seconds"] = [0.0, duration]
        timing["approximate"] = True
        timing["approximation"] = TIMING_METHOD
        timing.pop("grid_beat_count", None)
        timing.pop("grid_count_delta_from_zero_plus_measured", None)
        item["harmony_retiming_method"] = HARMONY_RETIMING_METHOD
        item["timeline_sha256"] = digest(timeline_path)

    manifest["timing_policy"] = TIMING_METHOD
    results = [verify_track(track, item) for track, item in zip(config["tracks"], manifest["tracks"])]
    save_json(manifest_path, manifest)
    print(json.dumps({
        "rebuilt_metadata_only": True,
        "audio_rewritten": False,
        "tracks": results,
        "metadata_sha256": {
            "EndingTracks.json": digest(manifest_path),
            **{item["timeline"]: item["timeline_sha256"] for item in manifest["tracks"]},
        },
    }, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare", action="store_true", help="Write full-length WAVs, timelines, and manifest")
    parser.add_argument("--analyze-only", action="store_true", help="Analyze beat estimates without writing assets")
    parser.add_argument("--rebuild-maps", action="store_true", help="Rebuild ending score maps from saved beat provenance")
    args = parser.parse_args()
    if sum((args.prepare, args.analyze_only, args.rebuild_maps)) > 1:
        parser.error("Choose only one operation")
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    if args.prepare:
        prepare(config)
        return
    if args.rebuild_maps:
        rebuild_maps(config)
        return
    target_lufs = float(ffmpeg_measure(ROOT / config["target_audio"])["input_i"])
    rows = []
    for track in config["tracks"]:
        analysis = analyze_track({**track, "sample_rate": config["sample_rate"]})
        rows.append({
            "name": track["name"], "seconds": analysis["source_seconds"],
            "declared_bpm_prior": analysis["declared_bpm"],
            "median_measured_beat_bpm": analysis["median_beat_bpm"],
            "measured_beats": analysis["measured_beat_count"],
            "timeline_beats_including_start_boundary": analysis["timeline_beat_count"],
            "bars": analysis["bars"],
        })
    print(json.dumps({"target_lufs": target_lufs, "analysis": rows}, indent=2))


if __name__ == "__main__":
    main()
