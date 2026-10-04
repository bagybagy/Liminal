import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const directory = path.join(root, 'Assets/Liminal/Resources/StageAudio');
const approved = JSON.parse(fs.readFileSync(path.join(directory, 'ApprovedTracks.json'), 'utf8'));
const expected = new Map([[1, 'Serpent_VelvetKeys'], [3, 'Hermit_OrchestralCurrent'],
  [4, 'Submarine_OrganicCurrent'], [5, 'Ending_BreathingLine']]);
const results = [];
assert.equal(approved.tracks.length, 4);

function waveChunks(wav) {
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF');
  assert.equal(wav.toString('ascii', 8, 12), 'WAVE');
  const chunks = new Map();
  for (let offset = 12; offset + 8 <= wav.length;) {
    const size = wav.readUInt32LE(offset + 4);
    chunks.set(wav.toString('ascii', offset, offset + 4), wav.subarray(offset + 8, offset + 8 + size));
    offset += 8 + size + (size % 2);
  }
  return chunks;
}

function validateMarks(marks, sampleCount) {
  assert.equal(marks[0], 0);
  for (let i = 0; i < marks.length; i++) {
    assert.ok(Number.isInteger(marks[i]) && marks[i] >= 0 && marks[i] < sampleCount);
    if (i) assert.ok(marks[i] > marks[i - 1]);
  }
}

for (const track of approved.tracks) {
  assert.equal(track.name, expected.get(track.theme_id));
  assert.equal(track.reference_audio, null);
  assert.equal(track.source_audio, null);
  assert.equal(track.generation_task, 'text2music');
  const wavPath = path.join(root, track.asset);
  const wav = fs.readFileSync(wavPath);
  const score = JSON.parse(fs.readFileSync(path.join(directory, track.name + 'Timeline.json'), 'utf8'));
  const chunks = waveChunks(wav), fmt = chunks.get('fmt '), pcm = chunks.get('data');
  assert.equal(fmt.readUInt16LE(0), 1);
  assert.equal(fmt.readUInt16LE(2), 2);
  assert.equal(fmt.readUInt32LE(4), 44100);
  assert.equal(fmt.readUInt16LE(14), 16);
  assert.equal(pcm.length / 4, score.sampleCount);
  assert.equal(score.sourceSha256, createHash('sha256').update(wav).digest('hex'));
  assert.equal(score.themeId, track.theme_id);
  assert.equal(score.theme, track.name);
  assert.equal(score.sampleRate, 44100);
  assert.equal(score.beats.length, score.bars * 4);
  assert.equal(score.eighths.length, score.beats.length * 2);
  validateMarks(score.beats, score.sampleCount);
  validateMarks(score.eighths, score.sampleCount);
  validateMarks(score.sections, score.sampleCount);
  for (let i = 0; i < score.beats.length; i++) {
    const end = score.beats[i + 1] ?? score.sampleCount;
    assert.equal(score.eighths[i * 2], score.beats[i]);
    assert.ok(Math.abs(score.eighths[i * 2 + 1] - (score.beats[i] + end) / 2) <= .5);
  }
  assert.equal(score.harmony[0].sample, 0);
  assert.ok(score.harmony.every(chord => chord.notes.length >= 3 && chord.notes[0] < chord.notes[1] && chord.notes[1] < chord.notes[2]));
  const importer = fs.readFileSync(wavPath + '.meta', 'utf8');
  assert.match(importer, /loadType: 0/);
  assert.match(importer, /sampleRateOverride: 44100/);
  assert.match(importer, /preloadAudioData: 1/);
  let peak = 0, energy = 0;
  for (let i = 0; i < pcm.length; i += 2) {
    const value = pcm.readInt16LE(i) / 32768;
    peak = Math.max(peak, Math.abs(value));
    energy += value * value;
  }
  assert.ok(peak < .999 && Math.sqrt(energy / (pcm.length / 2)) > .005);
  assert.equal(pcm.readInt16LE(0), 0);
  assert.equal(pcm.readInt16LE(pcm.length - 2), 0);
  results.push({ themeId: track.theme_id, name: track.name, seconds: score.sampleCount / score.sampleRate,
    bars: score.bars, measuredTempoBpm: score.tempoBpm, gainDb: track.gain_db, peak,
    harmonyIsEstimated: true, downbeatIsEstimated: true });
}
const tidal = fs.readFileSync(path.join(root, 'Assets/Liminal/Audio/TidalMemory.wav'));
const mainScore = JSON.parse(fs.readFileSync(path.join(root, 'Assets/Liminal/Resources/TidalMemoryTimeline.json'), 'utf8'));
assert.equal(createHash('sha256').update(tidal).digest('hex'), mainScore.sourceSha256);
console.log(JSON.stringify({ passed: true, firstAndWhaleUseUnchangedTidalMemory: true, tracks: results }, null, 2));
