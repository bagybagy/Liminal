import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { Renderer, RATE, BPM, BEAT, readWav } from './music-review/renderer.mjs';
import { scores } from './music-review/scores.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const folder = path.join(root, 'MusicReview', '02-ContrastingTrio');
const sampleDirectory = path.join(root, 'Tools', 'music-samples');
const selected = process.argv.slice(2);
if (selected.some(id => !scores.some(s => s.id === id))) throw new Error('Unknown score ID');
fs.mkdirSync(folder, { recursive: true });
const hash = filename => createHash('sha256').update(fs.readFileSync(filename)).digest('hex');

function ffmpeg(args) {
  const result = spawnSync('ffmpeg', ['-hide_banner', '-nostdin', '-y', ...args],
    { encoding: 'utf8', maxBuffer: 4 * 1024 * 1024, windowsHide: true });
  if (result.error || result.status !== 0) throw new Error(result.error?.message ?? result.stderr);
  return result.stderr;
}
function loudness(filename) {
  const log = ffmpeg(['-i', filename, '-af', 'loudnorm=I=-16.2:TP=-1.5:LRA=20:print_format=json', '-f', 'null', '-']);
  const start = log.lastIndexOf('{'), end = log.lastIndexOf('}');
  if (start < 0 || end < start) throw new Error('Missing FFmpeg loudness report');
  const result = JSON.parse(log.slice(start, end + 1));
  for (const key of ['input_i', 'input_tp', 'input_lra', 'input_thresh', 'target_offset'])
    if (!Number.isFinite(Number(result[key]))) throw new Error(`Invalid loudness measurement ${key}`);
  return result;
}
function master(raw, filename, measurement) {
  const filter = 'loudnorm=I=-16.2:TP=-1.5:LRA=20:linear=true:print_format=json'
    + `:measured_I=${measurement.input_i}:measured_TP=${measurement.input_tp}`
    + `:measured_LRA=${measurement.input_lra}:measured_thresh=${measurement.input_thresh}`
    + `:offset=${measurement.target_offset}`;
  const log = ffmpeg(['-i', raw, '-af', filter, '-ar', String(RATE), '-c:a', 'pcm_s16le', filename]);
  const match = log.slice(log.lastIndexOf('{'), log.lastIndexOf('}') + 1);
  return JSON.parse(match).normalization_type;
}
function stereoStatistics(filename) {
  const mono = readWav(filename);
  // Decode through FFmpeg so RIFF chunk placement and MP3 codec delay are not guessed.
  const result = spawnSync('ffmpeg', ['-hide_banner', '-nostdin', '-i', filename, '-f', 'f32le', '-ac', '2', '-ar', String(RATE), '-'],
    { maxBuffer: 200 * 1024 * 1024, windowsHide: true });
  if (result.error || result.status !== 0) throw new Error(result.error?.message ?? result.stderr.toString());
  const data = result.stdout;
  let clipped = 0, finite = true, l2 = 0, r2 = 0, lr = 0, peak = 0, side = 0, mid = 0;
  for (let i = 0; i < data.length; i += 8) {
    const l = data.readFloatLE(i), r = data.readFloatLE(i + 4);
    finite &&= Number.isFinite(l) && Number.isFinite(r);
    if (Math.abs(l) >= .99997) clipped++;
    if (Math.abs(r) >= .99997) clipped++;
    peak = Math.max(peak, Math.abs(l), Math.abs(r));
    l2 += l * l; r2 += r * r; lr += l * r;
    side += ((l - r) / 2) ** 2; mid += ((l + r) / 2) ** 2;
  }
  if (!finite || clipped) throw new Error(`Bad final samples: finite=${finite}, clips=${clipped}`);
  return { frames: mono.frames, durationSeconds: mono.frames / RATE, samplePeak: peak,
    rms: Math.sqrt((l2 + r2) / (data.length / 4)), clippedSamples: clipped,
    stereoCorrelation: lr / Math.sqrt(l2 * r2), sideToMidDb: 10 * Math.log10(side / mid) };
}

for (const spec of scores.filter(s => !selected.length || selected.includes(s.id))) {
  const started = Date.now();
  console.log(`Composing ${spec.title}: ${spec.bars} bars`);
  const renderer = new Renderer(spec, spec.acoustic ? sampleDirectory : null);
  spec.compose(renderer);
  const raw = path.join(folder, `${spec.id}.raw.wav`);
  const wav = path.join(folder, `${spec.id}.wav`), mp3 = path.join(folder, `${spec.id}.mp3`);
  const rawMetrics = renderer.finish(raw);
  const before = loudness(raw);
  const normalization = master(raw, wav, before);
  const after = loudness(wav);
  const stats = stereoStatistics(wav);
  if (Math.abs(Number(after.input_i) + 16.2) > .3) throw new Error('Master loudness missed target');
  if (Number(after.input_tp) > -1.0) throw new Error('Insufficient true-peak headroom');
  if (stats.frames !== renderer.frames) throw new Error('Mastering changed the musical grid length');
  ffmpeg(['-i', wav, '-ar', String(RATE), '-c:a', 'libmp3lame', '-b:a', '256k',
    '-metadata', `title=${spec.title}`, '-metadata', 'artist=tete / LIMINAL',
    '-metadata', 'comment=Audition candidate; listener approval required.', mp3]);
  // MP3 needs its own true-peak measurement: a safe WAV peak does not guarantee a safe encode.
  const encoded = loudness(mp3);
  if (Number(encoded.input_tp) > -.7) throw new Error('MP3 encoding overshoot');
  const sections = spec.sections.map(([bar, name]) => ({ bar, name, seconds: bar * 4 * BEAT,
    sample: Math.round(bar * 4 * BEAT * RATE) }));
  const timeline = { title: spec.title, bpm: BPM, timeSignature: [4, 4], bars: spec.bars,
    sampleRate: RATE, sampleCount: renderer.frames, sourceSha256: hash(wav),
    beats: Array.from({ length: spec.bars * 4 }, (_, i) => Math.round(i * BEAT * RATE)),
    sections, harmony: renderer.harmony, events: renderer.events.sort((a, b) => a.sample - b.sample) };
  const metrics = { title: spec.title, character: spec.character, bpm: BPM, timeSignature: [4, 4],
    bars: spec.bars, ...stats, integratedLufs: Number(after.input_i), truePeakDbtp: Number(after.input_tp),
    loudnessRangeLu: Number(after.input_lra), normalization,
    mp3: { integratedLufs: Number(encoded.input_i), truePeakDbtp: Number(encoded.input_tp), sha256: hash(mp3) },
    sourceSha256: timeline.sourceSha256, rawMetrics, seed: spec.seed, sections,
    instruments: [...new Set(renderer.events.map(e => e.instrument))],
    recordedSamples: Object.fromEntries(renderer.sampleUsage),
    method: 'New authored scores; Node.js synthesis and CC0 sample playback; FFmpeg two-pass mastering.',
    authoring: 'Codex in the current session. Sample preparation delegated to GPT-6 Luna.',
    externalMusicGenerationModel: null,
    status: 'Audition only. Musical quality requires listener review; not installed in the game.' };
  fs.writeFileSync(path.join(folder, `${spec.id}.timeline.json`), JSON.stringify(timeline));
  fs.writeFileSync(path.join(folder, `${spec.id}.metrics.json`), JSON.stringify(metrics, null, 2));
  fs.unlinkSync(raw);
  console.log(JSON.stringify({ title: spec.title, seconds: stats.durationSeconds, lufs: metrics.integratedLufs,
    truePeak: metrics.truePeakDbtp, normalization, renderSeconds: (Date.now() - started) / 1000 }));
}
