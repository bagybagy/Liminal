from pathlib import Path
import subprocess, math
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
SOURCES = [
    ("2026-10-04_23-59-43", Path(r"C:\Users\sutea\Videos\Captures\LIMINAL 2026-10-04 23-59-43.mp4")),
    ("2026-10-04_14-16-59", Path(r"C:\Users\sutea\Videos\Captures\LIMINAL 2026-10-04 14-16-59.mp4")),
]
INTERVAL = 30

def probe_duration(path):
    return float(subprocess.check_output(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(path)], text=True).strip())

for slug, source in SOURCES:
    if not source.exists():
        raise FileNotFoundError(source)
    folder = OUT / "samples" / slug
    folder.mkdir(parents=True, exist_ok=True)
    duration = probe_duration(source)
    times = list(range(0, math.floor(duration), INTERVAL))
    thumbs = []
    for sec in times:
        target = folder / f"t{sec:04d}.jpg"
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-ss", str(sec), "-i", str(source), "-frames:v", "1", "-vf", "scale=480:-2", "-q:v", "4", str(target)], check=True)
        thumbs.append((sec, Image.open(target).convert("RGB")))
    cell_w, cell_h, label_h, cols = 480, 270, 30, 3
    rows = math.ceil(len(thumbs) / cols)
    sheet = Image.new("RGB", (cols * cell_w, rows * (cell_h + label_h)), "#11151c")
    draw = ImageDraw.Draw(sheet)
    for i, (sec, img) in enumerate(thumbs):
        x, y = (i % cols) * cell_w, (i // cols) * (cell_h + label_h)
        img.thumbnail((cell_w, cell_h))
        sheet.paste(img, (x, y))
        draw.rectangle((x, y + cell_h, x + cell_w, y + cell_h + label_h), fill="#11151c")
        draw.text((x + 10, y + cell_h + 7), f"{sec//60:02d}:{sec%60:02d}  ({sec}s)", fill="white")
    outpath = OUT / f"contact_{slug}.jpg"
    sheet.save(outpath, quality=90, optimize=True)
    print(f"{slug}: duration={duration:.2f}s frames={len(thumbs)} sheet={outpath}")
