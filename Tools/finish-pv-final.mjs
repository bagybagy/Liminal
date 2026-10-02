import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.join(root, 'Video', 'PV');
const work = path.join(output, 'final-work');
fs.mkdirSync(work, { recursive: true });
const raw = path.join(output, 'LIMINAL_raw.mp4');
const capture = JSON.parse(fs.readFileSync(path.join(output, 'LIMINAL_raw.json'), 'utf8'));
const score = JSON.parse(fs.readFileSync(path.join(root, 'Assets', 'Liminal', 'Resources', 'TidalMemoryTimeline.json'), 'utf8'));
const high = path.join(output, 'Liminal_PV_High.mp4');
const light = path.join(output, 'Liminal_PV_Git.mp4');

function run(tool, args) {
  const result = spawnSync(tool, args, { encoding: 'utf8', windowsHide: true, maxBuffer: 32 * 1024 * 1024 });
  if (result.error || result.status !== 0) throw new Error(`${tool}: ${result.error?.message ?? result.stderr}`);
  return result;
}
function probe(file) {
  return JSON.parse(run('ffprobe', ['-v', 'error', '-show_streams', '-show_format', '-of', 'json', file]).stdout);
}
const rawInfo = probe(raw);
if (capture.frameRate !== 30 || capture.dolphinFullLocks !== 8 || Math.abs(capture.audioOffsetSeconds) > 2)
  throw new Error('The verified normal-speed, full-lock source is required.');

const shots = [
  { name: 'grotto', first: 0, last: 8 },
  { name: 'serpent', first: 48, last: 64 },
  { name: 'whale-arrival', first: 68, last: 92 },
  { name: 'whale-colour-and-peeling', first: 104, last: 120 },
  { name: 'dolphin-eight-lock-volley', first: 128, last: 144 },
];
let duration = 0;
const graph = [], videoInputs = [], audioInputs = [];
for (const [i, shot] of shots.entries()) {
  shot.sourceStart = capture.audioOffsetSeconds + score.beats[shot.first] / score.sampleRate;
  shot.duration = (score.beats[shot.last] - score.beats[shot.first]) / score.sampleRate;
  shot.outputStart = duration;
  shot.sourceFirstFrame = Math.round(shot.sourceStart * 30);
  shot.outputFirstFrame = Math.round(duration * 30);
  shot.frameCount = Math.round((duration + shot.duration) * 30) - shot.outputFirstFrame;
  if (shot.sourceStart < 0 || shot.sourceStart + shot.duration > Number(rawInfo.format.duration))
    throw new Error(`Source does not cover ${shot.name}.`);
  graph.push(`[0:v]trim=start_frame=${shot.sourceFirstFrame}:end_frame=${shot.sourceFirstFrame + shot.frameCount},setpts=PTS-STARTPTS[v${i}]`);
  graph.push(`[0:a]atrim=start=${shot.sourceStart}:duration=${shot.duration},asetpts=PTS-STARTPTS,afade=t=in:d=0.006,afade=t=out:st=${shot.duration - 0.006}:d=0.006[a${i}]`);
  videoInputs.push(`[v${i}]`);
  audioInputs.push(`[a${i}]`);
  duration += shot.duration;
}
if (duration < 30 || duration > 40) throw new Error('PV must be 30-40 seconds.');

function assTime(seconds) {
  const n = Math.round(seconds * 100);
  return `${Math.floor(n / 360000)}:${String(Math.floor(n / 6000) % 60).padStart(2, '0')}:${String(Math.floor(n / 100) % 60).padStart(2, '0')}.${String(n % 100).padStart(2, '0')}`;
}
const events = [];
function event(style, start, end, tags, text, layer = 1) {
  events.push(`Dialogue: ${layer},${assTime(start)},${assTime(end)},${style},,0,0,0,,{${tags}}${text}`);
}
function typewrite(text, start, end, x, y) {
  const letters = Array.from(text);
  const step = 0.095;
  for (let i = 1; i < letters.length; i++) {
    event('Poem', start + (i - 1) * step, start + i * step, `\\an7\\pos(${x},${y})`, letters.slice(0, i).join(''));
  }
  event('Poem', start + (letters.length - 1) * step, end, `\\an7\\pos(${x},${y})\\fad(0,380)`, text);
}

// Short left-aligned reveals stay clear of the central reticle and lower HUD.
event('OpeningBrand', 0.15, 2.8, '\\an7\\pos(122,738)\\fad(220,320)', 'LIMINAL');
typewrite('\u97f3\u3092\u3001\u6cf3\u3050\u3002', 0.75, 3.6, 120, 826);
typewrite('\u4e00\u6483\u304c\u3001\u5149\u306b\u306a\u308b\u3002', 4.45, 7.7, 120, 826);
typewrite('\u305d\u306e\u5149\u306f\u3001\u4e16\u754c\u306b\u9084\u308b\u3002', 24.0, 27.9, 120, 826);
event('Title', 35.35, duration - 0.1, '\\an7\\move(120,722,120,712,0,650)\\fad(450,180)', 'LIMINAL');
event('Subtitle', 35.85, duration - 0.1, '\\an7\\pos(124,840)\\fad(350,180)', 'ABYSSAL CHOIR');
event('Credit', 36.35, duration - 0.1, '\\an7\\pos(126,892)\\fad(300,180)', 'a game by tete');

const titles = `[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 2
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Poem,Yu Gothic,42,&H00F3FAF4,&H00F3FAF4,&H801B1710,&H901B1710,0,0,0,0,100,100,0,0,1,0.8,1.2,7,120,120,100,1
Style: OpeningBrand,Bahnschrift,66,&H00F3FAF4,&H00F3FAF4,&H801B1710,&H901B1710,0,0,0,0,100,100,7,0,1,0.8,1.2,7,120,120,100,1
Style: Title,Bahnschrift,96,&H00F3FAF4,&H00F3FAF4,&H801B1710,&H901B1710,0,0,0,0,100,100,12,0,1,0.8,1.2,7,120,120,100,1
Style: Subtitle,Consolas,25,&H00DEC89A,&H00DEC89A,&H801B1710,&H901B1710,0,0,0,0,100,100,4,0,1,0.6,0.8,7,120,120,100,1
Style: Credit,Yu Gothic,23,&H00CFD1CA,&H00CFD1CA,&H801B1710,&H901B1710,0,0,0,0,100,100,0,0,1,0.6,0.8,7,120,120,100,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
${events.join('\n')}
`;
const titlePath = path.join(output, 'final-titles.ass');
fs.writeFileSync(titlePath, titles, 'utf8');

const joinedAudio = `${graph.filter(x => x.startsWith('[0:a]')).join(';')};${audioInputs.join('')}concat=n=${shots.length}:v=0:a=1,afade=t=out:st=${duration - 0.55}:d=0.55`;
const measured = run('ffmpeg', ['-hide_banner', '-loglevel', 'info', '-i', raw,
  '-filter_complex', `${joinedAudio},loudnorm=I=-16:LRA=18:TP=-1.5:print_format=json[measure]`,
  '-map', '[measure]', '-f', 'null', '-']).stderr;
const loudness = JSON.parse(measured.slice(measured.lastIndexOf('{'), measured.lastIndexOf('}') + 1));
const normalizer = `loudnorm=I=-16:LRA=18:TP=-1.5:linear=true:measured_I=${loudness.input_i}:measured_LRA=${loudness.input_lra}:measured_TP=${loudness.input_tp}:measured_thresh=${loudness.input_thresh}:offset=${loudness.target_offset}`;

const filterFile = path.join(work, 'final.filter');
const assForFilter = titlePath.replaceAll('\\', '/').replaceAll(':', '\\:');
graph.push(`${videoInputs.join('')}concat=n=${shots.length}:v=1:a=0,eq=contrast=1.01:brightness=0.003:saturation=1.06:gamma=1.055,fps=30,trim=end_frame=${Math.round(duration * 30)},setpts=N/(30*TB),setsar=1,fade=t=out:st=${duration - 0.45}:d=0.45,subtitles=filename='${assForFilter}',format=yuv420p[vout]`);
graph.push(`${audioInputs.join('')}concat=n=${shots.length}:v=0:a=1,afade=t=out:st=${duration - 0.55}:d=0.55,${normalizer},aresample=48000[aout]`);
fs.writeFileSync(filterFile, graph.join(';'), 'utf8');
console.log(`Finishing ${duration.toFixed(2)}s: five normal-speed gameplay scenes, TidalMemory + original SE, restrained typewriter titles.`);
if (!process.argv.includes('--verify-only')) {
run('ffmpeg', ['-hide_banner', '-loglevel', 'warning', '-y', '-i', raw,
  '-filter_complex_threads', '2', '-filter_complex_script', filterFile, '-map', '[vout]', '-map', '[aout]',
  '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-maxrate', '14M', '-bufsize', '28M', '-threads', '6', '-r', '30', '-fps_mode', 'cfr',
  '-c:a', 'aac', '-b:a', '256k', '-ar', '48000', '-ac', '2', '-movflags', '+faststart',
  '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', high]);
run('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-i', high,
  '-vf', 'scale=1280:720:flags=lanczos', '-c:v', 'libx264', '-preset', 'medium', '-crf', '21',
  '-maxrate', '5M', '-bufsize', '10M', '-threads', '6', '-r', '30', '-fps_mode', 'cfr', '-c:a', 'aac', '-b:a', '192k',
  '-movflags', '+faststart', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', light]);
}

const summaries = [];
for (const [file, width, height] of [[high, 1920, 1080], [light, 1280, 720]]) {
  const info = probe(file), video = info.streams.find(x => x.codec_type === 'video');
  const audio = info.streams.find(x => x.codec_type === 'audio'), seconds = Number(info.format.duration);
  const bytes = fs.statSync(file).size;
  if (video?.width !== width || video.height !== height || video.avg_frame_rate !== '30/1' ||
      video.codec_name !== 'h264' || audio?.codec_name !== 'aac' || audio.sample_rate !== '48000' ||
      audio.channels !== 2 || Math.abs(seconds - duration) > 0.08 || bytes > 95 * 1024 * 1024)
    throw new Error(`Delivery validation failed: ${file}`);
  run('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-i', file, '-f', 'null', '-']);
  summaries.push({ file: path.basename(file), width, height, fps: 30, seconds, bytes });
}

const reviewTimes = [1.6, 5.6, 9.5, 14.5, 20.0, 26.0, 31.6, 37.1];
const frames = [];
for (const [i, seconds] of reviewTimes.entries()) {
  const frame = path.join(work, `review-${i}.png`);
  run('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-ss', String(seconds), '-i', high,
    '-frames:v', '1', '-vf', 'scale=640:360', frame]);
  frames.push(frame);
}
const contact = path.join(output, 'contact-sheet.jpg');
run('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...frames.flatMap(file => ['-i', file]),
  '-filter_complex', `${frames.map((_, i) => `[${i}:v]`).join('')}xstack=inputs=8:layout=0_0|648_0|1296_0|1944_0|0_368|648_368|1296_368|1944_368:fill=black[v]`,
  '-map', '[v]', '-frames:v', '1', contact]);
run('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-ss', '37.1', '-i', high,
  '-frames:v', '1', path.join(output, 'final-title.png')]);

fs.writeFileSync(path.join(output, 'final-edit.json'), JSON.stringify({
  durationSeconds: duration, source: 'LIMINAL_raw.mp4', metadata: 'LIMINAL_raw.json',
  sourceDate: '2026-10-01', sourceCapture: 'Unity Recorder 5.1.3 / Unity 6000.3.13f1',
  nativeGameSpeed: true, authoredBeatCuts: true, shots, titles: path.basename(titlePath),
  audio: 'Original captured TidalMemory and gameplay SE; phrase edits and measured loudness normalization, no newly generated music',
  loudnessTargetLufs: -16, truePeakTargetDb: -1.5, loudnessMeasurement: loudness,
  sourceFired: capture.fired, sourceHits: capture.hits, sourceDolphinFullLocks: capture.dolphinFullLocks,
  sourceCountsAreFullRawTakeNotEditedPvCounts: true, outputs: summaries,
}, null, 2), 'utf8');
fs.writeFileSync(path.join(output, 'validation.json'), JSON.stringify({
  passed: true, source: 'Unity Recorder source retained; FFmpeg / libass final edit',
  edit: 'final-edit.json', native_game_speed: true, audio_and_video_use_same_source_intervals: true,
  video_boundaries_quantized_to_nearest_30fps_frame: true,
  source_statistics_are_raw_take_statistics_not_edited_pv_statistics: true,
  audio: 'TidalMemory and original gameplay SE, normalized, no new soundtrack',
  target_lufs: -16, target_true_peak_db: -1.5, full_decode_checked: true, outputs: summaries,
  visual_review_assets: ['contact-sheet.jpg', 'final-title.png'],
}, null, 2), 'utf8');
console.log(JSON.stringify({ passed: true, outputs: summaries, contactSheet: contact }));
