from pathlib import Path
import subprocess, math
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parent
VIDEOS = {
    "clean": Path(r"C:\Users\sutea\Videos\Captures\LIMINAL 2026-10-04 23-59-43.mp4"),
    "vr": Path(r"C:\Users\sutea\Videos\Captures\LIMINAL 2026-10-04 14-16-59.mp4"),
}
RANGES = {
    "clean": [(0, 18), (78, 108), (112, 142), (235, 270), (320, 355), (380, 415), (495, 530), (555, 590), (600, 635), (660, 695), (710, 745), (775, 810), (825, 860), (865, 898)],
    "vr": [(0, 20), (65, 95), (105, 135), (135, 165), (205, 235), (235, 265), (280, 310), (350, 380), (410, 440), (440, 470), (490, 520), (550, 580), (590, 620)],
}
for kind, video in VIDEOS.items():
    times = sorted({t for a,b in RANGES[kind] for t in range(a,b+1,6)})
    frames=[]
    for sec in times:
        tmp=ROOT/"samples"/f"detail_{kind}_{sec:04d}.jpg"
        tmp.parent.mkdir(parents=True,exist_ok=True)
        subprocess.run(["ffmpeg","-hide_banner","-loglevel","error","-y","-ss",str(sec),"-i",str(video),"-frames:v","1","-vf","scale=640:-2","-q:v","3",str(tmp)],check=True)
        frames.append((sec,Image.open(tmp).convert("RGB")))
    cw,ch,label,cols=640,344,28,3
    rows=math.ceil(len(frames)/cols)
    sheet=Image.new("RGB",(cw*cols,(ch+label)*rows),"#11151c")
    d=ImageDraw.Draw(sheet)
    for i,(sec,img) in enumerate(frames):
        x=(i%cols)*cw;y=(i//cols)*(ch+label)
        img.thumbnail((cw,ch));sheet.paste(img,(x,y))
        d.text((x+8,y+ch+5),f"{sec//60:02d}:{sec%60:02d} ({sec}s)",fill="white")
    dest=ROOT/f"detail_{kind}.jpg";sheet.save(dest,quality=90)
    print(f"{kind}: {len(frames)} frames -> {dest}")
