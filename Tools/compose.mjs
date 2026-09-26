import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Original deterministic score: every arrangement boundary is shared with Score.cs.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const rate = 44100, beat = 60 / 124, bars = 104, duration = bars * 4 * beat;
const n = Math.ceil(duration * rate), left = new Float32Array(n), right = new Float32Array(n);
const tau = Math.PI * 2;
let seed = 41319;
const rand = () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; };
const hz = midi => 440 * 2 ** ((midi - 69) / 12);
function note(start, length, midi, gain, type, pan = 0) {
  const begin = Math.round(start * rate), count = Math.round(length * rate), f = hz(midi);
  const gl = Math.sqrt((1 - pan) / 2) * gain, gr = Math.sqrt((1 + pan) / 2) * gain;
  let filtered = 0;
  for (let i = 0; i < count && begin + i < n; i++) {
    const t = i / rate, x = i / count, phase = tau * f * t;
    let v = 0, env = 0;
    if (type === 'pad') {
      env = Math.min(t / 0.8, 1) * Math.min((length - t) / 1.8, 1);
      v = Math.sin(phase + 0.14 * Math.sin(t * 1.7)) * 0.55 +
          Math.sin(phase * 1.0021 + 0.9) * 0.23 + Math.sin(phase * 0.998 + 2) * 0.22 +
          Math.sin(phase * 2) * 0.08;
      v *= 0.87 + 0.13 * Math.sin(tau * t / (beat * 4));
    } else if (type === 'bell') {
      env = Math.min(t / 0.008, 1) * Math.exp(-t * 3.8) * Math.min((length - t) / 0.06, 1);
      v = Math.sin(phase + Math.sin(phase * 2) * 1.2 * Math.exp(-t * 9)) * 0.8 + Math.sin(phase * 3) * 0.1;
    } else if (type === 'bass') {
      env = Math.min(t / 0.006, 1) * Math.exp(-t * 2.4) * Math.min((length - t) / 0.05, 1);
      v = Math.sin(phase) * 0.78 + Math.sin(phase * 2) * 0.16 + Math.sin(phase * 3) * 0.06;
    } else if (type === 'kick') {
      env = Math.min(t / 0.002, 1) * Math.exp(-t * 10);
      v = Math.sin(tau * (45 * t + 8.4 * (1 - Math.exp(-t * 38)))) + (rand() * 2 - 1) * Math.exp(-t * 180) * 0.22;
    } else if (type === 'snare') {
      env = Math.min(t / 0.001, 1) * Math.exp(-t * 21);
      filtered += 0.32 * ((rand() * 2 - 1) - filtered);
      v = (rand() * 2 - 1 - filtered) * 0.62 + Math.sin(tau * 181 * t) * Math.exp(-t * 32) * 0.3;
    } else if (type === 'hat') {
      env = Math.min(t / 0.001, 1) * Math.exp(-t * (length > 0.15 ? 27 : 80));
      const noise = rand() * 2 - 1;
      filtered += 0.75 * (noise - filtered);
      v = noise - filtered;
    } else if (type === 'shimmer') {
      env = Math.sin(Math.PI * x) ** 2;
      v = Math.sin(phase) * Math.sin(phase * 1.0007) * 0.5 + Math.sin(phase * 2.003) * 0.2;
    }
    left[begin + i] += v * env * gl; right[begin + i] += v * env * gr;
  }
}
const chords = [[50,57,60,65,69], [46,53,57,60,65], [53,60,64,67,69], [48,55,58,62,67],
                [43,50,57,58,62], [46,53,57,60,65], [50,57,60,65,69], [48,55,58,62,67]];
const melody = [0, 2, 4, 3, 2, 1, 3, 4, 2, 4, 1, 3, 0, 1, 4, 2];
for (let bar = 0; bar < bars; bar++) {
  const start = bar * 4 * beat, chord = chords[Math.floor(bar / 2) % chords.length];
  const intro = bar < 4, breakdown = bar >= 52 && bar < 56, outro = bar >= 96;
  const force = bar >= 80 && !outro ? 1.2 : bar >= 56 && !outro ? 1.08 : 1;
  if (bar % 2 === 0) {
    chord.slice(1).forEach((m, i) => note(start, 8 * beat + 2, m + 12, 0.052 * (intro ? 0.65 : 1), 'pad', (i - 1.5) * 0.48));
    note(start + beat, 7 * beat, chord[3] + 24, 0.019, 'shimmer', Math.sin(bar) * 0.8);
  }
  if (!intro && !outro && !breakdown) {
    for (let q = 0; q < 4; q++) {
      if (bar < 8 && q % 2 !== 0) continue;
      note(start + q * beat, 0.55, 0, 0.49 * force, 'kick');
      if (q % 2 === 1 && bar >= 8) note(start + q * beat, 0.27, 0, 0.17, 'snare', 0.13);
      note(start + (q + 0.5) * beat, 0.21, 0, 0.23 * force, 'hat', -0.25);
      note(start + (q + 0.75) * beat, 0.08, 0, 0.12, 'hat', 0.48);
      note(start + (q + 0.25) * beat, beat * 0.62, chord[0] - 12 + (q === 3 && bar % 4 === 3 ? 12 : 0), 0.23, 'bass');
    }
  }
  if (bar >= 8 && !outro) {
    const steps = bar >= 56 && !breakdown ? 8 : 4;
    for (let s = 0; s < steps; s++) {
      if ((s + bar) % 7 === 0) continue;
      const m = chord[melody[(bar * 4 + s) % melody.length]] + (bar >= 80 ? 24 : 12);
      note(start + (s * 4 / steps + 0.5) * beat, 1.7, m, 0.062 * (s % 2 === 0 ? 1 : 0.63), 'bell', Math.sin(bar * 1.3 + s) * 0.7);
    }
  }
  if (bar % 8 === 7 && !outro) {
    for (let s = 0; s < 4; s++) note(start + (3 + s / 4) * beat, 0.12, 0, 0.09 + s * 0.02, 'snare', (s - 1.5) * 0.35);
  }
}
// Cross-feedback delays make the synth voices part of one continuous acoustic space.
const dl = Math.round(beat * 0.75 * rate), dr = Math.round(beat * 1.25 * rate);
let lowL = 0, lowR = 0, peak = 0, energy = 0;
const output = Buffer.alloc(44 + n * 4);
output.write('RIFF', 0); output.writeUInt32LE(output.length - 8, 4); output.write('WAVEfmt ', 8);
output.writeUInt32LE(16, 16); output.writeUInt16LE(1, 20); output.writeUInt16LE(2, 22);
output.writeUInt32LE(rate, 24); output.writeUInt32LE(rate * 4, 28); output.writeUInt16LE(4, 32);
output.writeUInt16LE(16, 34); output.write('data', 36); output.writeUInt32LE(n * 4, 40);
for (let i = 0; i < n; i++) {
  lowL += 0.18 * ((i > dl ? right[i - dl] : 0) - lowL);
  lowR += 0.18 * ((i > dr ? left[i - dr] : 0) - lowR);
  left[i] += lowL * 0.24; right[i] += lowR * 0.24;
  const t = i / rate, fade = Math.min(t / 3, 1) * Math.min((duration - t) / 8, 1);
  const l = Math.tanh(left[i] * 1.5) * fade * 0.9, r = Math.tanh(right[i] * 1.5) * fade * 0.9;
  peak = Math.max(peak, Math.abs(l), Math.abs(r)); energy += (l*l + r*r) / 2;
  output.writeInt16LE(Math.round(l * 32767), 44 + i * 4);
  output.writeInt16LE(Math.round(r * 32767), 46 + i * 4);
}
const dest = path.join(root, 'Assets/Liminal/Audio');
fs.mkdirSync(dest, { recursive: true });
fs.writeFileSync(path.join(dest, 'TidalMemory.wav'), output);
console.log(JSON.stringify({title:'Tidal Memory', bpm:124, bars, duration, sampleRate:rate, peak, rms:Math.sqrt(energy/n), bytes:output.length}));
