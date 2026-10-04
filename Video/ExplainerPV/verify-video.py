"""Validate the delivered movie once: stream metadata, full decode and loudness."""
from pathlib import Path
import json
import subprocess
import sys

root = Path(__file__).resolve().parent
movie = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "out" / "LIMINAL_ABYSSAL_CHOIR_ExplainerPV_1080p.mp4"
probe = subprocess.run(["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json", str(movie)], capture_output=True, text=True, check=True)
data = json.loads(probe.stdout)
video = next(s for s in data["streams"] if s["codec_type"] == "video")
audio = next(s for s in data["streams"] if s["codec_type"] == "audio")
duration = float(data["format"]["duration"])
assert abs(duration - 90) < 0.15, duration
assert (video["width"], video["height"]) == (1920, 1080)
assert video["codec_name"] == "h264" and video["pix_fmt"] == "yuv420p"
assert video.get("color_range") == "tv" and video.get("color_space") == "bt709"
assert video["r_frame_rate"] == "30/1"
assert audio["codec_name"] == "aac" and audio["channels"] == 2
assert movie.stat().st_size < 95 * 1024 * 1024, "Delivery exceeds 95 MiB"
decode = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-xerror", "-i", str(movie), "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"], capture_output=True, text=True, encoding="utf-8", errors="replace", check=True)
log = decode.stderr
loudness = json.loads(log[log.rfind("{"):log.rfind("}") + 1])
assert -17.0 <= float(loudness["input_i"]) <= -15.0, loudness
assert float(loudness["input_tp"]) < -0.5, loudness
report = {"file": movie.name, "durationSeconds": duration, "width": video["width"], "height": video["height"], "fps": video["r_frame_rate"], "videoCodec": video["codec_name"], "colorSpace": video["color_space"], "colorRange": video["color_range"], "audioCodec": audio["codec_name"], "audioChannels": audio["channels"], "sizeMiB": round(movie.stat().st_size / 1024 / 1024, 2), "integratedLUFS": float(loudness["input_i"]), "truePeakDBTP": float(loudness["input_tp"]), "fullDecodePassed": True}
(root / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
