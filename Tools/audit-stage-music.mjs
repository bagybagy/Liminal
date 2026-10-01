import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const directory = path.join(root, 'Assets/Liminal/Resources/StageAudio');
const names = ['JellyGrotto', 'SerpentSanctum', 'HorizonWhale', 'TidalShells', 'ScarletEngine', 'Afterglow'];
const results = [];
const harmonicPaths = new Set();
const tonalPitchClasses = new Set([0, 2, 4, 5, 7, 9, 10]);
let sharedPivotChord;

for (let themeId = 0; themeId < names.length; themeId++) {
  const name = names[themeId];
  const wavPath = path.join(directory, name + '.wav');
  const wav = fs.readFileSync(wavPath);
  const score = JSON.parse(fs.readFileSync(path.join(directory, name + 'Timeline.json'), 'utf8'));
  const importerMeta = fs.readFileSync(wavPath + '.meta', 'utf8');
  assert.ok(wav.length < 100 * 1024 * 1024, name + ' source WAV stays below GitHub file limit');
  assert.match(importerMeta, /loadType: 0/, name + ' Decompress On Load importer setting');
  assert.match(importerMeta, /sampleRateSetting: 2[\s\S]*sampleRateOverride: 44100/, name + ' 44.1 kHz importer setting');
  assert.match(importerMeta, /compressionFormat: 1[\s\S]*quality: 0\.9/, name + ' Vorbis quality importer setting');
  assert.match(importerMeta, /preloadAudioData: 1/, name + ' preloaded importer setting');
  assert.equal(score.themeId, themeId, name + ' theme id');
  assert.equal(score.theme, name, name + ' theme name');
  assert.equal(score.bpm, 124, name + ' tempo');
  assert.equal(score.bars, 64, name + ' arrangement length');
  assert.equal(score.sampleRate, 44100, name + ' sample rate');
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF', name + ' WAV header');
  assert.equal(wav.toString('ascii', 8, 12), 'WAVE', name + ' WAV format');
  assert.equal(wav.readUInt16LE(20), 1, name + ' PCM encoding');
  assert.equal(wav.readUInt16LE(22), 2, name + ' stereo channel count');
  assert.equal(wav.readUInt32LE(24), score.sampleRate, name + ' WAV sample rate');
  assert.equal(wav.readUInt32LE(40) / 4, score.sampleCount, name + ' PCM frame count');
  assert.equal(createHash('sha256').update(wav).digest('hex'), score.sourceSha256, name + ' timeline hash');

  const expectedFrames = Math.round(score.bars * 4 * 60 / score.bpm * score.sampleRate);
  assert.equal(score.sampleCount, expectedFrames, name + ' exact loop frame count');
  assert.equal(score.beats.length, score.bars * 4, name + ' beat count');
  assert.equal(score.eighths.length, score.bars * 8, name + ' eighth count');
  for (let i = 0; i < score.beats.length; i++)
    assert.equal(score.beats[i], Math.round(i * 60 / score.bpm * score.sampleRate), name + ' beat ' + i);
  for (let i = 0; i < score.eighths.length; i++)
    assert.equal(score.eighths[i], Math.round(i * 30 / score.bpm * score.sampleRate), name + ' eighth ' + i);
  assert.equal(score.harmony.length, score.bars / 4, name + ' chord changes');
  assert.ok(score.events.length > score.bars, name + ' authored arrangement events');
  score.harmony.forEach((chord, i) => assert.equal(chord.sample, Math.round(i * 16 * 60 / score.bpm * score.sampleRate), name + ' chord sample ' + i));
  const pivotCore = score.harmony[0].notes.slice(0, 3).join(',');
  if (sharedPivotChord == null) sharedPivotChord = pivotCore;
  assert.equal(pivotCore, sharedPivotChord, name + ' shared D-minor pivot chord');
  assert.ok(score.harmony.every(chord => chord.notes.every(note => tonalPitchClasses.has(note % 12))), name + ' stays in the shared D-minor tonal world');
  const opening = score.events.filter(event => event.sample < Math.round(2 * 4 * 60 / score.bpm * score.sampleRate));
  assert.deepEqual(opening.map(event => [event.sample, event.type, event.midi]), [
    [0, 'pad', 62], [0, 'pad', 69]
  ], name + ' opens with the sparse tonic-fifth pivot before its arrangement enters');
  harmonicPaths.add(score.harmony.map(chord => chord.notes.join(',')).join('|'));

  let peak = 0, energy = 0, loopBoundaryJump = 0;
  const blockFrames = Math.round(score.sampleRate * 0.25), blockEnergy = new Float64Array(Math.ceil(score.sampleCount / blockFrames));
  let firstLeft = 0, firstRight = 0, lastLeft = 0, lastRight = 0;
  for (let i = 0; i < score.sampleCount; i++) {
    const left = wav.readInt16LE(44 + i * 4) / 32768;
    const right = wav.readInt16LE(46 + i * 4) / 32768;
    const frameEnergy = (left * left + right * right) * 0.5;
    peak = Math.max(peak, Math.abs(left), Math.abs(right));
    energy += frameEnergy;
    blockEnergy[Math.floor(i / blockFrames)] += frameEnergy;
    if (i === 0) { firstLeft = left; firstRight = right; }
    if (i === score.sampleCount - 1) { lastLeft = left; lastRight = right; }
  }
  loopBoundaryJump = Math.max(Math.abs(firstLeft - lastLeft), Math.abs(firstRight - lastRight));
  const rms = Math.sqrt(energy / score.sampleCount);
  const minimumBlockRms = Math.sqrt(Math.min(...blockEnergy) / blockFrames);
  assert.ok(peak < 0.90, name + ' has headroom');
  assert.ok(rms > 0.012, name + ' is not too quiet');
  assert.ok(minimumBlockRms > 0.00025, name + ' has no silent quarter-second blocks');
  assert.ok(loopBoundaryJump < 0.004, name + ' loop boundary is click-free');
  results.push({ theme: name, bars: score.bars, seconds: score.sampleCount / score.sampleRate, events: score.events.length,
    rms: Number(rms.toFixed(4)), peak: Number(peak.toFixed(4)), minQuarterSecondRms: Number(minimumBlockRms.toFixed(4)),
    loopBoundaryJump: Number(loopBoundaryJump.toFixed(5)) });
}

assert.equal(harmonicPaths.size, names.length, 'Each stage has a distinct authored chord path');
console.log(JSON.stringify({ themes: results, distinctHarmonicPaths: harmonicPaths.size }, null, 2));
