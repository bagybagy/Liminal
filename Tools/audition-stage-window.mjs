import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readWav } from './music-review/renderer.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const names = { hermit: 'Hermit_OrchestralCurrent', submarine: 'Submarine_OrganicCurrent' };
const results = [];
for (const [stage, name] of Object.entries(names)) {
  const base = path.join(root, 'Assets/Liminal/Resources/StageAudio', name);
  const score = JSON.parse(fs.readFileSync(base + 'Timeline.json', 'utf8'));
  const wav = readWav(base + '.wav');
  if (wav.rate !== score.sampleRate || wav.frames !== score.sampleCount)
    throw new Error('Timeline and recording differ: ' + name);
  const sum = (first, last) => {
    let energy = 0;
    for (let i = first; i < last; i++) energy += wav.mono[i] ** 2;
    return energy;
  };
  const candidates = [];
  const beats = 24;
  for (let beat = 0; beat + beats < score.beats.length; beat += 16) {
    const first = score.beats[beat], last = score.beats[beat + beats];
    if (first < wav.frames / 3 || last > wav.frames * 2 / 3) continue;
    const barRms = [];
    let energy = 0;
    for (let bar = 0; bar < beats; bar += 4) {
      const a = score.beats[beat + bar], b = score.beats[beat + bar + 4];
      const e = sum(a, b);
      energy += e;
      barRms.push(Math.sqrt(e / (b - a)));
    }
    const rms = Math.sqrt(energy / (last - first));
    const sustainedScore = .75 * rms + .25 * Math.min(...barRms);
    candidates.push({ startBeat: beat, beats, startSeconds: first / wav.rate,
      endSeconds: last / wav.rate, rms, barRms, sustainedScore });
  }
  candidates.sort((a, b) => b.sustainedScore - a.sustainedScore);
  if (!candidates.length) throw new Error('No middle-third musical phrase: ' + stage);
  results.push({ stage, recording: name, durationSeconds: wav.frames / wav.rate,
    selectionMethod: 'Sustained mono RMS in middle third; four-bar-aligned starts, six-bar window. Loudness proxy, not a human-verified musical climax.',
    introRms: Math.sqrt(sum(score.beats[0], score.beats[32]) / (score.beats[32] - score.beats[0])),
    selected: candidates[0], alternatives: candidates.slice(1, 3) });
}
console.log(JSON.stringify(results, null, 2));
