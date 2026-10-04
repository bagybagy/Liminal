"""Cut user-provided recordings without modifying originals; assemble matched audio."""
from pathlib import Path
import argparse
import json
import subprocess
import shutil


def run(args):
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(result.stderr[-6000:])
    return result.stderr


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    target = root / "public" / "media"
    work = root / "work"
    target.mkdir(parents=True, exist_ok=True)
    work.mkdir(exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("ffmpeg is required on PATH")
    audio_files = []
    for shot in plan["shots"]:
        source = Path(shot["source"])
        if not source.is_file():
            raise FileNotFoundError(source)
        name = shot["id"]
        length = float(shot["durationSeconds"])
        video_filter = shot.get("crop", "")
        if video_filter:
            video_filter += ","
        video_filter += "scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080,setsar=1,fps=30"
        prefix = [ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-ss", str(shot["startSeconds"]), "-i", str(source), "-t", str(length)]
        run(prefix + ["-map", "0:v:0", "-an", "-vf", video_filter, "-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p", "-threads", "4", "-movflags", "+faststart", str(target / f"{name}.mp4")])
        audio = work / f"{name}.wav"
        fade_out = max(0, length - 0.18)
        af = f"aresample=48000,apad,atrim=duration={length},asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.10,afade=t=out:st={fade_out}:d=0.18"
        run(prefix + ["-map", "0:a:0", "-vn", "-af", af, "-ac", "2", "-ar", "48000", "-c:a", "pcm_s16le", str(audio)])
        audio_files.append(audio)
    listing = work / "audio-concat.txt"
    listing.write_text("\n".join("file '" + p.as_posix().replace("'", "'\\''") + "'" for p in audio_files), encoding="utf-8")
    stitched = work / "audio-stitched.wav"
    run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(listing), "-c:a", "pcm_s16le", str(stitched)])
    measured = run([ffmpeg, "-hide_banner", "-i", str(stitched), "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"])
    values = json.loads(measured[measured.rfind("{"):measured.rfind("}") + 1])
    normalizer = "loudnorm=I=-16:TP=-1.5:LRA=11:linear=true:measured_I={input_i}:measured_TP={input_tp}:measured_LRA={input_lra}:measured_thresh={input_thresh}:offset={target_offset}".format(**values)
    run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(stitched), "-af", normalizer, "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(target / "mix.wav")])
    report = {"shotCount": len(plan["shots"]), "durationSeconds": sum(s["durationSeconds"] for s in plan["shots"]), "audio": "media/mix.wav", "audioMeasurement": values, "originalsModified": False}
    (work / "media-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
