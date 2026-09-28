import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const wav = fs.readFileSync(path.join(root, 'Assets/Liminal/Audio/TidalMemory.wav'));
const score = JSON.parse(fs.readFileSync(path.join(root, 'Assets/Liminal/Resources/TidalMemoryTimeline.json')));
assert.equal(createHash('sha256').update(wav).digest('hex'), score.sourceSha256, 'Timeline belongs to a different recording');
assert.equal(wav.readUInt32LE(24), score.sampleRate);
assert.equal(wav.readUInt32LE(40) / 4, score.sampleCount);
assert.equal(wav.readUInt16LE(20), 1, 'Audit expects the composer PCM WAV');
const rate = score.sampleRate, hop = 44, energy = new Float64Array(Math.ceil(score.sampleCount / hop));
// Inspect the rendered mix, independently of scheduler math: low-frequency attacks
// in kick windows versus the off-beat phase. Peaks follow onset because of attack envelopes.
const alpha = 1 - Math.exp(-2 * Math.PI * 160 / rate);
let low = 0;
for (let i = 0; i < score.sampleCount; i++) {
  const sample = (wav.readInt16LE(44 + i * 4) + wav.readInt16LE(46 + i * 4)) / 65536;
  low += alpha * (sample - low);
  energy[Math.floor(i / hop)] += low * low / hop;
}
const flux = i => {
  let before = 0, after = 0;
  for (let k = 0; k < 8; k++) { before += energy[i - k - 1] ?? 0; after += energy[i + k] ?? 0; }
  return Math.max(0, after - before);
};
const offsets = [], onBeat = [], offBeat = [];
for (const event of score.events.filter(e => e.type === 'kick' && e.sample > 16 * rate)) {
  const bin = Math.round(event.sample / hop);
  let best = -Infinity, bestAt = 0;
  for (let j = -75; j <= 75; j++) {
    const value = flux(bin + j);
    if (value > best) { best = value; bestAt = j; }
  }
  offsets.push(bestAt * hop / rate * 1000);
  onBeat.push(Math.max(...Array.from({length:35}, (_, j) => flux(bin + j - 5))));
  const halfBeat = Math.round((60 / 124) * rate / hop / 2);
  offBeat.push(Math.max(...Array.from({length:35}, (_, j) => flux(bin + halfBeat + j - 5))));
}
offsets.sort((a,b) => a-b);
const mean = a => a.reduce((sum, x) => sum + x, 0) / a.length;
const result = {
  sourceSha256:score.sourceSha256, samples:score.sampleCount, sampleRate:rate,
  detectedKickWindows:offsets.length,
  attackFluxMedianMs:offsets[Math.floor(offsets.length * .5)],
  attackFluxP95Ms:offsets[Math.floor(offsets.length * .95)],
  onBeatToOffBeatLowFrequencyFlux:mean(onBeat) / mean(offBeat),
  loopRoundingMicroseconds:(score.sampleCount / rate - 104 * 4 * 60 / 124) * 1e6,
  note:'Waveform attack-energy measurement; not speaker, Bluetooth or display latency.'
};
assert(Math.abs(result.attackFluxMedianMs) < 25, 'Rendered mix has a material beat-phase offset');
assert(result.onBeatToOffBeatLowFrequencyFlux > 2, 'Quarter-note phase lacks a stronger kick onset than the off beat');
fs.mkdirSync(path.join(root, 'Verification'), {recursive:true});
fs.writeFileSync(path.join(root, 'Verification/music-audit.json'), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result, null, 2));
