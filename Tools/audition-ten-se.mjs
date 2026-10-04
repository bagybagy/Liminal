import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { Renderer, RATE, BEAT, readWav, writeWav } from './music-review/renderer.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const exportHarp = process.argv.includes('--export-harp-bank');
const folder = path.join(root, 'MusicReview/14-TenSePalette');
const sampleFolder = path.join(root, 'Tools/se-audition-samples');
const asset = 'Assets/Liminal/Resources/StageAudio/Serpent_VelvetKeys';
const score = JSON.parse(fs.readFileSync(path.join(root, asset + 'Timeline.json'), 'utf8'));
const manifest = JSON.parse(fs.readFileSync(path.join(sampleFolder, 'samples.json'), 'utf8'));
const oldSamples = JSON.parse(fs.readFileSync(path.join(root, 'Tools/music-samples/samples.json'), 'utf8'));
const fold = [0, 2, 1, 0, 1, 2, 1, 0];
const voices = [
  { id: '01_Piano', label: 'ピアノ', sample: 'piano' },
  { id: '02_Glockenspiel', label: 'グロッケン（鉄琴）', sample: 'bell_keys' },
  { id: '03_Marimba', label: 'マリンバ', sample: 'marimba' },
  { id: '04_TubularBells', label: 'チューブラーベル（鐘）', sample: 'vibraphone' },
  { id: '05_Harp', label: 'ハープ', sample: 'harp' },
  { id: '06_Pizzicato', label: '弦のピチカート', sample: 'pizzicato' },
  { id: '07_BreathFlute', label: '短いフルート', sample: 'flute' },
  { id: '08_ElectricKeys', label: '柔らかな電子鍵盤', synth: 'keys' },
  { id: '09_DigitalPluck', label: '短いデジタル・プラック', synth: 'pluck' },
  { id: '10_RoundDrop', label: '丸い水滴風の音', synth: 'tom' }
];
const first = score.beats[32], length = score.beats[48] - first;
const intro = Math.round(RATE * 2.1), total = intro + length;
if (!exportHarp) fs.mkdirSync(folder, { recursive: true });

function hash(bytes) { return createHash('sha256').update(bytes).digest('hex'); }
function ffmpeg(args, binary = false) {
  const result = spawnSync('ffmpeg', ['-hide_banner', '-nostdin', '-loglevel', 'error', '-y', ...args],
    { encoding: binary ? undefined : 'utf8', maxBuffer: 32 * 1024 * 1024, windowsHide: true });
  if (result.error || result.status !== 0) throw new Error(result.error?.message ?? result.stderr.toString());
  return result.stdout;
}
function floats(bytes) {
  if (bytes.length % 4) throw new Error('Invalid float PCM length');
  const pcm = new Float32Array(bytes.length / 4);
  for (let i = 0; i < pcm.length; i++) pcm[i] = bytes.readFloatLE(i * 4);
  return pcm;
}
const bgm = exportHarp ? null : floats(ffmpeg(['-i', path.join(root, asset + '.wav'), '-af',
  `atrim=start_sample=${first}:end_sample=${first + length},asetpts=PTS-STARTPTS`,
  '-ac', '2', '-ar', String(RATE), '-f', 'f32le', '-'], true));
if (!exportHarp && bgm.length !== length * 2) throw new Error('BGM sample crop length changed');

const sources = {}, cache = new Map(), prototype = new Renderer({ bars: 1, seed: 171, duck: 0 }, null);
for (const voice of voices.filter(v => v.sample && (!exportHarp || v.sample === 'harp'))) {
  const entries = voice.sample === 'flute' ? oldSamples.instruments.flute : manifest.instruments[voice.sample];
  if (!entries?.length) throw new Error('Missing recorded instrument: ' + voice.sample);
  sources[voice.sample] = entries.map(entry => {
    const filename = path.join(voice.sample === 'flute' ? path.join(root, 'Tools/music-samples') : sampleFolder, entry.file);
    const wav = readWav(filename);
    const threshold = wav.peak * .02;
    let onset = 0;
    while (onset < wav.frames && Math.abs(wav.mono[onset]) < threshold) onset++;
    onset = Math.max(0, onset - Math.round(wav.rate * .003));
    return { ...entry, filename, onset, rate: wav.rate, sourceSha256: hash(fs.readFileSync(filename)) };
  });
  voice.recordedInstrument = manifest.names?.[voice.sample] ?? voice.label;
}

function noteAt(index, sample) {
  let chord = score.harmony[0];
  for (const candidate of score.harmony) {
    if (candidate.sample > sample) break;
    chord = candidate;
  }
  let midi = chord.notes[fold[index]] + 12;
  while (midi > 78) midi -= 12;
  while (midi < 60) midi += 12;
  return midi;
}
function tone(voice, midi) {
  const key = voice.id + ':' + midi;
  if (cache.has(key)) return cache.get(key);
  const count = Math.round(RATE * 1.2), left = new Float32Array(count), right = new Float32Array(count);
  let selected;
  if (voice.sample) {
    selected = sources[voice.sample].reduce((a, b) => Math.abs(b.midi - midi) < Math.abs(a.midi - midi) ? b : a);
    const rate = Math.round(selected.rate * 2 ** ((midi - selected.midi) / 12));
    // Use FFmpeg's band-limited resampler rather than interpolation that can alias.
    const pcm = floats(ffmpeg(['-i', selected.filename, '-af',
      `atrim=start_sample=${selected.onset},asetpts=PTS-STARTPTS,asetrate=${rate},aresample=${RATE},apad=whole_len=${count},atrim=end_sample=${count}`,
      '-ac', '1', '-f', 'f32le', '-'], true));
    if (pcm.length !== count) throw new Error('Sampled tone length changed');
    for (let i = 0; i < count; i++) {
      const t = i / RATE;
      const attack = Math.min(t / (voice.sample === 'flute' ? .025 : .003), 1);
      const gate = t <= .78 ? 1 : Math.max(0, (1.2 - t) / .42) ** 1.5;
      left[i] = right[i] = pcm[i] * attack * gate;
    }
  } else {
    prototype.left.fill(0); prototype.right.fill(0); prototype.events.length = 0;
    prototype.seed = 171;
    prototype.note(0, 0, midi, .78 / BEAT, .06, voice.synth, { release: .42, decay: .6, send: 0 });
    left.set(prototype.left.subarray(0, count)); right.set(prototype.right.subarray(0, count));
  }
  let energy = 0, peak = 0;
  for (let i = 0; i < count; i++) {
    energy += (left[i] ** 2 + right[i] ** 2) / (2 * RATE);
    peak = Math.max(peak, Math.abs(left[i]), Math.abs(right[i]));
  }
  if (energy < 1e-8 || !Number.isFinite(energy)) throw new Error('Invalid tone: ' + key);
  const gain = Math.min(Math.sqrt(.010 / energy), .48 / peak);
  for (let i = 0; i < count; i++) { left[i] *= gain; right[i] *= gain; }
  const result = { left, right, source: selected ? { file: path.relative(root, selected.filename).replaceAll('\\', '/'),
    rootMidi: selected.midi, onsetTrimSamples: selected.onset, sha256: selected.sourceSha256 } : { generator: voice.synth },
    gain, energy: energy * gain ** 2, peak: peak * gain };
  cache.set(key, result);
  return result;
}
function add(left, right, sound, firstFrame, gain) {
  for (let i = 0; i < sound.left.length && firstFrame + i < left.length; i++) {
    left[firstFrame + i] += sound.left[i] * gain;
    right[firstFrame + i] += sound.right[i] * gain;
  }
}
function edgeFade(left, right) {
  const edge = Math.round(RATE * .015);
  for (let i = 0; i < left.length; i++) {
    const gain = Math.min(i / edge, (left.length - 1 - i) / edge, 1) * .8;
    left[i] *= gain; right[i] *= gain;
  }
}
function writeImporterMeta(filename, importer, folderAsset = false) {
  const meta = filename + '.meta';
  const previous = fs.existsSync(meta) ? fs.readFileSync(meta, 'utf8').match(/^guid: ([a-f0-9]{32})$/m)?.[1] : null;
  const guid = previous ?? hash(Buffer.from('Liminal:' + path.relative(root, filename).replaceAll('\\', '/'))).slice(0, 32);
  fs.writeFileSync(meta, `fileFormatVersion: 2\nguid: ${guid}\n${folderAsset ? 'folderAsset: yes\n' : ''}${importer}\n`);
}
function verifyBankWav(filename, sound) {
  const bytes = fs.readFileSync(filename), count = 52920;
  if (bytes.length !== 44 + count * 4 || bytes.toString('ascii', 0, 4) !== 'RIFF' ||
      bytes.toString('ascii', 8, 12) !== 'WAVE' || bytes.readUInt16LE(20) !== 1 ||
      bytes.readUInt16LE(22) !== 2 || bytes.readUInt32LE(24) !== 44100 ||
      bytes.readUInt16LE(34) !== 16 || bytes.readUInt32LE(40) !== count * 4)
    throw new Error('Bank WAV format changed: ' + filename);
  let maxError = 0, peak = 0, energy = 0;
  for (let i = 0; i < count; i++) {
    for (let c = 0; c < 2; c++) {
      const expected = c ? sound.right[i] : sound.left[i];
      const integer = bytes.readInt16LE(44 + i * 4 + c * 2), decoded = integer / 32767;
      if (!Number.isFinite(expected) || !Number.isFinite(decoded) || Math.abs(integer) >= 32767)
        throw new Error('Nonfinite or clipped bank note: ' + filename);
      maxError = Math.max(maxError, Math.abs(decoded - expected));
      peak = Math.max(peak, Math.abs(decoded));
      energy += decoded ** 2 / (2 * RATE);
    }
  }
  if (!(energy > 1e-8) || peak > .48 + 1 / 32767 || maxError > 1 / 32767)
    throw new Error('Silent, over-peak or inaccurate bank note: ' + filename);
  return { pcmPeak: peak, pcmEnergy: energy, pcmMaxError: maxError };
}
function exportHarpBank() {
  if (RATE !== 44100 || manifest.license !== 'CC0-1.0') throw new Error('Bank input format or license changed');
  const bank = path.join(root, 'Assets/Liminal/Resources/StageNoteAudio/SerpentHarp');
  const referenceFile = path.join(bank, 'AUDITION_REFERENCE.txt');
  const reference = JSON.parse(fs.readFileSync(referenceFile, 'utf8'));
  const toneSourceSha256 = hash(Buffer.from(tone.toString().replaceAll('\r\n', '\n')));
  if (reference.toneSourceSha256 !== toneSourceSha256 ||
      reference.sourceManifestSha256 !== hash(fs.readFileSync(path.join(sampleFolder, 'samples.json'))))
    throw new Error('Approved audition tone or source manifest changed');
  for (const input of reference.sources) {
    if (hash(fs.readFileSync(path.join(root, input.file))) !== input.sha256)
      throw new Error('Approved harp recording changed: ' + input.file);
  }
  const voice = voices.find(v => v.id === '05_Harp');
  const auditionMidis = [...new Set([noteAt(0, score.beats[36]),
    ...Array.from({ length: 8 }, (_, i) => noteAt(i, score.eighths[72 + i]))])].sort((a, b) => a - b);
  if (JSON.stringify(auditionMidis) !== JSON.stringify(reference.auditionMidis))
    throw new Error('Approved audition MIDI set changed');
  const defaultImporter = 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: ';
  const textImporter = 'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: ';
  const audioImporter = 'AudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n  defaultSettings:\n'
    + '    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 2\n    sampleRateOverride: 44100\n'
    + '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n'
    + '  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  loadInBackground: 0\n'
    + '  ambisonic: 0\n  3D: 1\n  userData: \n  assetBundleName: \n  assetBundleVariant: ';
  fs.mkdirSync(bank, { recursive: true });
  if (!fs.existsSync(path.dirname(bank) + '.meta')) writeImporterMeta(path.dirname(bank), defaultImporter, true);
  writeImporterMeta(bank, defaultImporter, true);
  const notes = [];
  for (let midi = 60; midi <= 78; midi++) {
    const sound = tone(voice, midi), file = `Note_${midi}.wav`, filename = path.join(bank, file);
    if (sound.left.length !== 52920 || sound.right.length !== 52920) throw new Error('Bank tone length changed');
    const floatLeftSha256 = hash(Buffer.from(sound.left.buffer));
    const floatRightSha256 = hash(Buffer.from(sound.right.buffer));
    const approved = reference.tones.find(t => t.midi === midi);
    if (approved && (approved.floatLeftSha256 !== floatLeftSha256 || approved.floatRightSha256 !== floatRightSha256))
      throw new Error('Float tone differs from approved audition: ' + midi);
    const metrics = writeWav(filename, sound.left, sound.right);
    const verification = verifyBankWav(filename, sound);
    writeImporterMeta(filename, audioImporter);
    const source = manifest.instruments.harp.find(entry => entry.midi === sound.source.rootMidi);
    notes.push({ midi, file, resourcePath: `StageNoteAudio/SerpentHarp/Note_${midi}`, sampleRate: RATE,
      channels: 2, bitsPerSample: 16, sampleCount: 52920, seconds: 1.2,
      sha256: hash(fs.readFileSync(filename)), floatLeftSha256, floatRightSha256,
      source: { ...sound.source, url: source.source }, gain: sound.gain,
      floatEnergy: sound.energy, floatPeak: metrics.peak, ...verification });
  }
  const repositoryFile = filename => ({ file: path.relative(root, filename).replaceAll('\\', '/'),
    link: path.relative(bank, filename).replaceAll('\\', '/'), sha256: hash(fs.readFileSync(filename)) });
  const proof = { passed: true, checkedNotes: notes.length, finite: true, silentNotes: 0, clippedSamples: 0,
    auditionMidis, approvedFloatHashMatches: reference.tones.length,
    maxPcmError: Math.max(...notes.map(note => note.pcmMaxError)), maxAllowedPcmError: 1 / 32767,
    maxPcmPeak: Math.max(...notes.map(note => note.pcmPeak)) };
  const provenance = { license: 'CC0-1.0', instrument: 'Harp', approvedAudition: '05_Harp',
    generator: repositoryFile(fileURLToPath(import.meta.url)), renderer: repositoryFile(path.join(root, 'Tools/music-review/renderer.mjs')),
    ffmpegVersion: ffmpeg(['-version']).split(/\r?\n/)[0],
    toneSourceSha256, sourceManifest: repositoryFile(path.join(sampleFolder, 'samples.json')),
    sourceDocumentation: repositoryFile(path.join(sampleFolder, 'SOURCES.md')),
    auditionReference: repositoryFile(referenceFile),
    originalAuditionGeneratorSha256: reference.originalGeneratorSha256,
    licenseFile: repositoryFile(path.join(bank, 'LICENSE.txt')),
    officialLicenseUrl: 'https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/LICENSE',
    processing: { sharedFunction: 'tone()', onsetThresholdFraction: .02, onsetPrerollSeconds: .003,
      resampling: 'atrim=start_sample=onset,asetpts=PTS-STARTPTS,asetrate=round(sourceRate*2^((midi-rootMidi)/12)),aresample=44100,apad=whole_len=52920,atrim=end_sample=52920',
      attackSeconds: .003, gateStartSeconds: .78, gateEndSeconds: 1.2, gatePower: 1.5,
      energyTarget: .010, peakCap: .48, gameVolumeBakedIn: false, masterVolumeBakedIn: false },
    unityImporter: { compressionFormat: 0, loadType: 0, preloadAudioData: 1, normalize: 0 },
    notes, verification: proof };
  const provenanceFile = path.join(bank, 'PROVENANCE.txt');
  fs.writeFileSync(provenanceFile, JSON.stringify(provenance, null, 2) + '\n');
  for (const filename of [referenceFile, path.join(bank, 'LICENSE.txt'), provenanceFile])
    writeImporterMeta(filename, textImporter);
  fs.writeSync(1, JSON.stringify({ output: path.relative(root, bank).replaceAll('\\', '/'), ...proof }) + '\n');
}
if (exportHarp) {
  exportHarpBank();
  process.exit(0);
}
const medleyL = new Float32Array(total * voices.length), medleyR = new Float32Array(total * voices.length);
const report = { candidateIntegratedInGame: false, sampleRate: RATE, musicalGridHumanVerified: false,
  sourceBgm: asset + '.wav', sourceSha256: hash(fs.readFileSync(path.join(root, asset + '.wav'))),
  sourceStartSeconds: first / RATE, introSeconds: intro / RATE, sampleLicense: 'CC0-1.0',
  noteLevelPolicy: 'Per-note energy target .010, peak cap .48; common music .83, SE .62, master .8; no individual mix normalization',
  clips: [] };
const fingerprints = new Set();
for (let index = 0; index < voices.length; index++) {
  const voice = voices[index], left = new Float32Array(total), right = new Float32Array(total);
  const soloMidi = noteAt(0, score.beats[36]), solo = tone(voice, soloMidi);
  const fingerprint = hash(Buffer.from(solo.left.buffer));
  if (fingerprints.has(fingerprint)) throw new Error('Two audition voices are identical');
  fingerprints.add(fingerprint);
  add(left, right, solo, Math.round(RATE * .12), .62);
  for (let i = 0; i < length; i++) {
    left[intro + i] += bgm[i * 2] * .83;
    right[intro + i] += bgm[i * 2 + 1] * .83;
  }
  const notes = [];
  for (let note = 0; note < 8; note++) {
    const sample = score.eighths[36 * 2 + note], midi = noteAt(note, sample), sound = tone(voice, midi);
    add(left, right, sound, intro + sample - first, .62);
    notes.push({ sample: intro + sample - first, midi, ...sound.source, energy: sound.energy, peak: sound.peak });
  }
  edgeFade(left, right);
  const wav = path.join(folder, voice.id + '.wav');
  const metrics = writeWav(wav, left, right);
  ffmpeg(['-i', wav, '-c:a', 'libmp3lame', '-b:a', '320k',
    '-metadata', `title=${voice.id} ${voice.label}`, path.join(folder, voice.id + '.mp3')]);
  medleyL.set(left, index * total); medleyR.set(right, index * total);
  report.clips.push({ id: voice.id, label: voice.label, recordedInstrument: voice.recordedInstrument, seconds: total / RATE,
    medleyStartSeconds: index * total / RATE, ...metrics, notes });
}
writeWav(path.join(folder, '00_AllTen.wav'), medleyL, medleyR);
ffmpeg(['-i', path.join(folder, '00_AllTen.wav'), '-c:a', 'libmp3lame', '-b:a', '320k', path.join(folder, '00_AllTen.mp3')]);
fs.writeFileSync(path.join(folder, 'study-report.json'), JSON.stringify(report, null, 2));
const timestamp = seconds => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
fs.writeFileSync(path.join(folder, 'INDEX.md'), '# 10種類のSE試聴\n\n[連続試聴](00_AllTen.mp3)\n\n'
  + '| 番号 | 音色 | 連続音源の開始 | 個別音源 |\n| --- | --- | --- | --- |\n'
  + report.clips.map((clip, i) => `| ${i + 1} | ${clip.label} | ${timestamp(clip.medleyStartSeconds)} | [試聴](${clip.id}.mp3) |`).join('\n') + '\n');
console.log(JSON.stringify({ output: folder, clips: report.clips.length, secondsEach: total / RATE,
  medleySeconds: total * voices.length / RATE, maxPeak: Math.max(...report.clips.map(c => c.peak)),
  uniqueVoices: fingerprints.size, candidateIntegratedInGame: false }));
