import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const rate = 44100, bpm = 124, beat = 60 / bpm, bars = 64, barSeconds = beat * 4;
const frames = Math.round(bars * barSeconds * rate), tau = Math.PI * 2;
const destination = path.join(root, 'Assets/Liminal/Resources/StageAudio');
const themes = [
  {
    id: 0, name: 'JellyGrotto', seed: 12013,
    chords: [[50,53,57,60,64], [46,53,57,62,65], [53,57,60,64,67], [48,55,58,62,65]],
    style: 'jelly'
  },
  {
    id: 1, name: 'SerpentSanctum', seed: 12037,
    chords: [[50,53,57,60,64], [48,55,58,62,65], [55,58,62,65,69], [45,52,55,60,64]],
    style: 'serpent'
  },
  {
    id: 2, name: 'HorizonWhale', seed: 12059,
    chords: [[50,53,57,60,64], [46,53,57,62,65], [48,55,58,62,65], [53,57,60,64,67]],
    style: 'whale'
  },
  {
    id: 3, name: 'TidalShells', seed: 12071,
    chords: [[50,53,57,60], [45,52,55,60], [55,58,62,65], [48,55,58,62,65]],
    style: 'hermit'
  },
  {
    id: 4, name: 'ScarletEngine', seed: 12097,
    chords: [[50,53,57,60], [46,53,57,62,65], [48,55,58,62,65], [55,58,62,65]],
    style: 'engine'
  },
  {
    id: 5, name: 'Afterglow', seed: 12109,
    chords: [[50,53,57,60,64], [48,55,58,62,65], [46,53,57,62,65], [50,53,57,60,64]],
    style: 'ending'
  }
];

const repriseIntervals = [3,7,10,7,5,3,2,0];
const hz = midi => 440 * 2 ** ((midi - 69) / 12);
const sampleAtBeat = value => Math.round(value * beat * rate);
const sampleAtBar = value => Math.round(value * barSeconds * rate);
const unityFolderMeta = () => 'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n';
function guidFor(assetPath) {
  return createHash('sha256').update('Liminal.StageMusic/' + assetPath.replaceAll('\\', '/')).digest('hex').slice(0, 32);
}
function writeMeta(file, kind) {
  const relative = path.relative(path.join(root, 'Assets'), file).replaceAll('\\', '/');
  const guid = guidFor(relative);
  let body;
  if (kind === 'folder') body = unityFolderMeta();
  else if (kind === 'json') body = 'fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n';
  else body = [
    'fileFormatVersion: 2', 'guid: {guid}', 'AudioImporter:', '  externalObjects: {}', '  serializedVersion: 8',
    '  defaultSettings:', '    serializedVersion: 2', '    loadType: 0', '    sampleRateSetting: 2', '    sampleRateOverride: 44100',
    '    compressionFormat: 1', '    quality: 0.9', '    conversionMode: 0', '    preloadAudioData: 1',
    '  platformSettingOverrides: {}', '  forceToMono: 0', '  normalize: 0', '  loadInBackground: 0', '  ambisonic: 0',
    '  3D: 0', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ', ''
  ].join('\n');
  fs.writeFileSync(file + '.meta', body.replace('{guid}', guid));
}

function render(theme) {
  const left = new Float32Array(frames), right = new Float32Array(frames), events = [];
  let seed = theme.seed >>> 0;
  const random = () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; };
  function note(start, length, midi, gain, type, pan = 0, attack = 0.01, release = 0.04) {
    const begin = Math.round(start * rate), count = Math.max(1, Math.round(length * rate));
    if (begin >= frames || begin + count <= 0) return;
    const f = midi > 0 ? hz(midi) : 0;
    const gl = Math.sqrt((1 - pan) / 2) * gain, gr = Math.sqrt((1 + pan) / 2) * gain;
    const end = Math.min(count, frames - begin);
    let filtered = 0;
    if (begin >= 0) events.push({ sample: begin, type, midi });
    for (let i = 0; i < end; i++) {
      const t = i / rate, x = i / count, phase = tau * f * t;
      let v = 0, env = 0;
      if (type === 'pad') {
        env = Math.min(t / attack, 1) * Math.min((length - t) / release, 1);
        v = Math.sin(phase + 0.10 * Math.sin(t * 0.71)) * 0.50 +
            Math.sin(phase * 1.0017 + 1.1) * 0.28 + Math.sin(phase * 0.9984 + 2.2) * 0.22 +
            Math.sin(phase * 2) * 0.055;
        v *= 0.91 + 0.09 * Math.sin(tau * t / (barSeconds * 2));
      } else if (type === 'glass') {
        env = Math.min(t / 0.012, 1) * Math.exp(-t * 2.6) * Math.min((length - t) / 0.045, 1);
        v = Math.sin(phase + Math.sin(phase * 2) * 0.82 * Math.exp(-t * 7)) * 0.74 + Math.sin(phase * 3.01) * 0.12;
      } else if (type === 'wood') {
        env = Math.min(t / 0.006, 1) * Math.exp(-t * 5.0) * Math.min((length - t) / 0.035, 1);
        v = Math.sin(phase) * 0.70 + Math.sin(phase * 2.08) * 0.23 * Math.exp(-t * 4) + Math.sin(phase * 3) * 0.07;
      } else if (type === 'arp') {
        env = Math.min(t / 0.008, 1) * Math.exp(-t * 3.4) * Math.min((length - t) / 0.04, 1);
        v = Math.sin(phase + 0.18 * Math.sin(t * 3)) * 0.72 + Math.sin(phase * 2.003) * 0.18;
      } else if (type === 'bass') {
        env = Math.min(t / 0.014, 1) * Math.exp(-t * 2.1) * Math.min((length - t) / 0.05, 1);
        v = Math.sin(phase) * 0.82 + Math.sin(phase * 2) * 0.14 + Math.sin(phase * 3) * 0.04;
      } else if (type === 'pulse') {
        env = Math.min(t / 0.003, 1) * Math.exp(-t * 9.0) * Math.min((length - t) / 0.012, 1);
        v = Math.sin(phase + 0.24 * Math.sin(phase * 0.5)) * 0.70 + Math.sin(phase * 2) * 0.12;
      } else if (type === 'tick') {
        env = Math.min(t / 0.001, 1) * Math.exp(-t * 75);
        const noise = random() * 2 - 1;
        filtered += 0.80 * (noise - filtered);
        v = noise - filtered;
      } else if (type === 'air') {
        env = Math.sin(Math.PI * x) ** 2;
        v = Math.sin(phase) * 0.58 + Math.sin(phase * 1.5 + 0.4) * 0.22 + Math.sin(phase * 2.001) * 0.12;
      }
      left[begin + i] += v * env * gl;
      right[begin + i] += v * env * gr;
    }
  }

  const chordForBar = bar => theme.chords[Math.floor(bar / 4) % theme.chords.length];
  const stageGain = bar => bar < 8 || bar >= 56 ? 0.72 : bar < 16 || bar >= 48 ? 0.88 : bar < 32 ? 1 : bar < 40 ? 1.08 : 0.96;
  for (let bar = 0; bar < bars; bar++) {
    const start = bar * barSeconds, chord = chordForBar(bar), energy = stageGain(bar), beatAt = offset => start + offset * beat;
    if (bar < 2) {
      if (bar === 0) {
        note(start, barSeconds * 4 - 0.18, 62, 0.018, 'pad', -0.15, 0.85, 0.72);
        note(start, barSeconds * 4 - 0.18, 69, 0.014, 'pad', 0.15, 1.0, 0.72);
      }
      continue;
    }
    if (bar % 4 === 0) {
      if (theme.style === 'whale') {
        chord.slice(0, 4).forEach((m, i) => note(start, barSeconds * 4 - 0.3, m + 12, 0.027 * energy, 'pad', (i - 1.5) * 0.47, 2.0, 1.5));
        note(start, barSeconds * 4 - 0.25, chord[0] - 12, 0.085 * energy, 'pad', 0, 1.2, 1.1);
      } else if (theme.style === 'ending') {
        chord.slice(0, 4).forEach((m, i) => note(start, barSeconds * 4 - 0.25, m + 12, 0.030 * energy, 'pad', (i - 1.5) * 0.42, 1.6, 1.25));
      } else {
        const padCount = theme.style === 'hermit' ? 3 : 4;
        chord.slice(0, padCount).forEach((m, i) => note(start, barSeconds * 4 - 0.22, m + 12, 0.024 * energy, 'pad', (i - (padCount - 1) / 2) * 0.42, 1.2, 0.85));
      }
      note(start, 2.8, chord[0] - 12, (theme.style === 'whale' ? 0.03 : 0.055) * energy, 'bass', 0, 0.12, 0.25);
    }

    if (theme.style === 'jelly') {
      if (bar % 2 === 0) {
        const motif = Math.floor(bar / 2) % repriseIntervals.length;
        const a = chord[0] + repriseIntervals[motif] + 24;
        note(beatAt(1) + 0.08, 1.65, a, 0.043 * energy, 'glass', -0.34, 0.02, 0.12);
        if (bar % 4 === 2) {
          const b = chord[0] + repriseIntervals[(motif + 1) % repriseIntervals.length] + 24;
          note(beatAt(3) + 0.12, 1.35, b, 0.032 * energy, 'glass', 0.42, 0.02, 0.12);
        }
      }
      if (bar % 4 === 3) note(beatAt(2.5), 0.62, chord[2] + 12, 0.021 * energy, 'air', -0.55, 0.22, 0.18);
    } else if (theme.style === 'serpent') {
      for (let step = 0; step < 8; step++) {
        if ((step + bar) % 7 === 0) continue;
        const tone = chord[(step + Math.floor(bar / 2)) % chord.length] + 12 + (step % 4 === 3 ? 12 : 0);
        note(beatAt(step / 2) + 0.035, 0.34, tone, (step % 2 ? 0.023 : 0.030) * energy,
          'arp', Math.sin((bar * 8 + step) * 0.15) * 0.55, 0.009, 0.035);
      }
      if (bar % 2 === 1) note(beatAt(2.5), 0.55, chord[0] - 12, 0.043 * energy, 'bass', 0, 0.015, 0.06);
    } else if (theme.style === 'whale') {
      if ([8, 20, 28, 36].includes(bar)) {
        [0, 2, 4, 7].forEach((step, i) => {
          const tone = chord[Math.min(i, chord.length - 1)] + 12 + (i > 1 ? 12 : 0);
          note(beatAt(step), 2.2, tone, 0.038 * energy, 'air', (i - 1.5) * 0.48, 0.35, 0.5);
        });
      }
      if (bar % 4 === 2) note(beatAt(2), 0.9, chord[1] + 12, 0.026 * energy, 'glass', 0.35, 0.03, 0.1);
    } else if (theme.style === 'hermit') {
      for (let step = 0; step < 4; step++) {
        const tone = chord[(step + Math.floor(bar / 2)) % chord.length] + 12 + (step === 3 ? 12 : 0);
        if (step !== 1 || bar % 2 === 0)
          note(beatAt(step) + (step === 2 ? 0.10 : 0), 0.46, tone, 0.040 * energy, 'wood', (step - 1.5) * 0.30, 0.006, 0.05);
        if ((bar + step) % 2 === 0)
          note(beatAt(step + 0.5), 0.24, 0, 0.018 * energy, 'tick', step % 2 ? 0.45 : -0.45);
      }
      note(beatAt(2.5), 0.42, chord[0] - 12, 0.045 * energy, 'bass', 0, 0.012, 0.05);
    } else if (theme.style === 'engine') {
      for (let step = 0; step < 4; step++) {
        if (bar < 8 && step % 2) continue;
        note(beatAt(step), 0.29, chord[0] - 12, 0.067 * energy, 'pulse', 0, 0.004, 0.035);
        if ((bar + step) % 3 !== 1)
          note(beatAt(step + 0.5), 0.10, 0, 0.020 * energy, 'tick', step % 2 ? 0.37 : -0.37);
      }
      for (let step = 0; step < 4; step++) {
        if ((bar + step) % 3 === 0) {
          const tone = chord[(step + bar) % chord.length] + 12;
          note(beatAt(step) + 0.17, 0.48, tone, 0.025 * energy, 'arp', step % 2 ? 0.33 : -0.33, 0.008, 0.04);
        }
      }
      if (bar % 4 === 3) note(beatAt(3.5), 0.22, 0, 0.025 * energy, 'tick', 0.18);
    } else {
      // A sparse, softer reprise of the Jelly Grotto interval motif.
      if (bar % 2 === 0) {
        const step = Math.floor(bar / 2) % repriseIntervals.length;
        const tone = chord[0] + repriseIntervals[step] + 24;
        note(beatAt(1) + 0.12, 2.0, tone, 0.029 * energy, 'glass', (step % 2 ? 0.35 : -0.35), 0.03, 0.18);
        if (bar % 4 === 2) {
          const answer = chord[0] + repriseIntervals[(step + 1) % repriseIntervals.length] + 24;
          note(beatAt(3) + 0.12, 1.8, answer, 0.021 * energy, 'glass', 0.48, 0.03, 0.18);
        }
      }
    }
  }

  const delayL = Math.round(beat * 0.75 * rate), delayR = Math.round(beat * 1.25 * rate);
  let lowL = 0, lowR = 0, peak = 0, energy = 0, loopEdgeDelta = 0;
  const output = Buffer.alloc(44 + frames * 4);
  output.write('RIFF', 0); output.writeUInt32LE(output.length - 8, 4); output.write('WAVEfmt ', 8);
  output.writeUInt32LE(16, 16); output.writeUInt16LE(1, 20); output.writeUInt16LE(2, 22);
  output.writeUInt32LE(rate, 24); output.writeUInt32LE(rate * 4, 28); output.writeUInt16LE(4, 32);
  output.writeUInt16LE(16, 34); output.write('data', 36); output.writeUInt32LE(frames * 4, 40);
  for (let i = 0; i < frames; i++) {
    lowL += 0.16 * ((i > delayL ? right[i - delayL] : 0) - lowL);
    lowR += 0.16 * ((i > delayR ? left[i - delayR] : 0) - lowR);
    left[i] += lowL * 0.19;
    right[i] += lowR * 0.19;
    const edgeFade = Math.min(i / (rate * 0.035), (frames - 1 - i) / (rate * 0.035), 1);
    left[i] *= edgeFade;
    right[i] *= edgeFade;
    peak = Math.max(peak, Math.abs(left[i]), Math.abs(right[i]));
  }
  const master = peak > 0.78 ? 0.78 / peak : 1;
  peak = 0;
  for (let i = 0; i < frames; i++) {
    const l = left[i] * master, r = right[i] * master;
    peak = Math.max(peak, Math.abs(l), Math.abs(r));
    energy += (l * l + r * r) / 2;
    const li = Math.max(-32768, Math.min(32767, Math.round(l * 32767)));
    const ri = Math.max(-32768, Math.min(32767, Math.round(r * 32767)));
    output.writeInt16LE(li, 44 + i * 4);
    output.writeInt16LE(ri, 46 + i * 4);
    if (i === 0) loopEdgeDelta = Math.max(Math.abs(li), Math.abs(ri));
    if (i === frames - 1) loopEdgeDelta = Math.max(loopEdgeDelta, Math.abs(li), Math.abs(ri));
  }
  events.sort((a, b) => a.sample - b.sample);
  const timeline = {
    themeId: theme.id, theme: theme.name, bpm, bars, sampleRate: rate, sampleCount: frames,
    sourceSha256: createHash('sha256').update(output).digest('hex'),
    beats: Array.from({ length: bars * 4 }, (_, i) => sampleAtBeat(i)),
    eighths: Array.from({ length: bars * 8 }, (_, i) => Math.round(i * beat * rate / 2)),
    sections: [0, 8, 16, 24, 32, 40].map(sampleAtBar),
    harmony: Array.from({ length: bars / 4 }, (_, i) => ({ sample: sampleAtBar(i * 4), notes: theme.chords[i % theme.chords.length] })),
    events
  };
  return {
    output, timeline,
    metrics: { theme: theme.name, bpm, bars, duration: frames / rate, sampleRate: rate, peak, rms: Math.sqrt(energy / frames), loopEdgeDelta }
  };
}

fs.mkdirSync(destination, { recursive: true });
const folder = path.dirname(destination);
if (!fs.existsSync(folder + '.meta')) writeMeta(folder, 'folder');
if (!fs.existsSync(destination + '.meta')) writeMeta(destination, 'folder');
const results = [];
for (const theme of themes) {
  const { output, timeline, metrics } = render(theme);
  const timelinePath = path.join(destination, theme.name + 'Timeline.json');
  fs.writeFileSync(timelinePath, JSON.stringify(timeline));
  const wavPath = path.join(destination, theme.name + '.wav');
  fs.writeFileSync(wavPath, output);
  writeMeta(wavPath, 'audio');
  writeMeta(timelinePath, 'json');
  results.push(metrics);
  console.log(JSON.stringify(metrics));
}
console.log(JSON.stringify({ themes: results.length, output: path.relative(root, destination) }));
