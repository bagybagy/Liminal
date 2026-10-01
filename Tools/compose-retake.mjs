import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const destination = path.join(root, 'MusicReview', '01-TidalBloom');
const rate = 44100, bpm = 124, beat = 60 / bpm, bars = 96;
const duration = bars * 4 * beat, frames = Math.round(duration * rate);
const tau = 2 * Math.PI, hz = midi => 440 * 2 ** ((midi - 69) / 12);
const left = new Float32Array(frames), right = new Float32Array(frames);
const sendL = new Float32Array(frames), sendR = new Float32Array(frames);
const events = [];
let seed = 6212026;
const random = () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; };

const chords = [
  [50, 57, 60, 65, 69], [46, 53, 57, 60, 65],
  [53, 60, 64, 67, 69], [48, 55, 58, 62, 67],
  [43, 50, 57, 58, 62], [46, 53, 57, 60, 65],
  [50, 57, 60, 65, 69], [48, 55, 58, 62, 67]
];
// Beat, MIDI pitch, held beats. The answer returns to D instead of a random chord tone.
const hook = [
  [[.5,74,.70],[1.5,77,.70],[2.5,81,.45],[3,79,.40],[3.5,77,.45]],
  [[.5,76,1.10],[2,74,.65],[3,72,.45],[3.5,69,.45]],
  [[.5,77,.70],[1.5,81,.70],[2.5,84,.65],[3.5,81,.45]],
  [[.5,77,.70],[1.5,74,.70],[2.5,72,.65],[3.5,69,.45]],
  [[.5,77,.70],[1.5,79,.65],[2.5,81,.45],[3,84,.40],[3.5,81,.45]],
  [[.5,79,1.10],[2,77,.65],[3,76,.45],[3.5,72,.45]],
  [[.5,79,.70],[1.5,76,.70],[2.5,74,.65],[3.5,72,.45]],
  [[.5,74,.70],[1.5,76,.70],[2.5,77,.45],[3,76,.40],[3.5,74,.75]]
];
const sections = [
  { bar:0, name:'Arrival' }, { bar:8, name:'First bloom' },
  { bar:24, name:'Current' }, { bar:40, name:'Suspension' },
  { bar:48, name:'Rising' }, { bar:56, name:'Full bloom' },
  { bar:80, name:'Return' }, { bar:88, name:'Afterimage' }
];
const chordAt = bar => chords[Math.floor(bar / 2) % chords.length];
const at = (bar, offset = 0) => (bar * 4 + offset) * beat;

function note(start, length, midi, gain, type, pan = 0, send = 0) {
  const begin = Math.round(start * rate), count = Math.round(length * rate);
  const f = midi ? hz(midi) : 0, gl = Math.sqrt((1 - pan) / 2) * gain;
  const gr = Math.sqrt((1 + pan) / 2) * gain;
  events.push({ sample:begin, type, midi, gain });
  let filtered = 0, low = 0;
  for (let i = 0; i < count && begin + i < frames; i++) {
    const t = i / rate, p = tau * f * t, tail = Math.min((length - t) / .035, 1);
    let v = 0, env = 0;
    if (type === 'pad') {
      env = Math.min(t / .48, 1) * Math.min((length - t) / 1.15, 1);
      v = Math.sin(p + .11 * Math.sin(t * 1.4)) * .55 +
        Math.sin(p * 1.0021 + .9) * .23 + Math.sin(p * .998 + 2) * .22 +
        Math.sin(p * 2) * .06;
    } else if (type === 'lead') {
      env = Math.min(t / .012, 1) * Math.exp(-t * 2.5) * tail;
      v = Math.sin(p + Math.sin(p * 2) * .86 * Math.exp(-t * 8)) * .80 +
        Math.sin(p * 2.001 + .3) * .12 * Math.exp(-t * 3) + Math.sin(p * 3) * .06;
    } else if (type === 'arp') {
      env = Math.min(t / .006, 1) * Math.exp(-t * 5.5) * tail;
      v = Math.sin(p + Math.sin(p * 2) * .55 * Math.exp(-t * 10)) * .85 + Math.sin(p * 3) * .08;
    } else if (type === 'bass') {
      env = Math.min(t / .006, 1) * Math.exp(-t * 2.2) * tail;
      v = Math.sin(p) * .74 + Math.sin(p * 2) * .19 + Math.sin(p * 3) * .07;
    } else if (type === 'kick') {
      env = Math.min(t / .0015, 1) * Math.exp(-t * 9.8);
      v = Math.sin(tau * (45 * t + 8.4 * (1 - Math.exp(-t * 38)))) +
        (random() * 2 - 1) * Math.exp(-t * 190) * .18;
    } else if (type === 'snare') {
      const noise = random() * 2 - 1;
      filtered += .32 * (noise - filtered);
      env = Math.min(t / .001, 1) * Math.exp(-t * 22);
      v = (noise - filtered) * .76 + Math.sin(tau * 181 * t) * Math.exp(-t * 27) * .26;
    } else if (type === 'clap') {
      const noise = random() * 2 - 1;
      filtered += .28 * (noise - filtered);
      low += .075 * (noise - low);
      env = Math.exp(-t * 22) * (.25 + .75 * Math.min(t / .007, 1));
      for (const onset of [0, .012, .023]) if (t >= onset && t < onset + .008)
        env += .50 * Math.exp(-(t - onset) * 220);
      v = (filtered - low) * 1.8;
    } else if (type === 'hat' || type === 'shaker') {
      const noise = random() * 2 - 1;
      filtered += .70 * (noise - filtered);
      env = Math.min(t / .001, 1) * Math.exp(-t * (type === 'shaker' ? 105 : length > .15 ? 28 : 85));
      v = (noise - filtered) * (type === 'shaker' ? .80 : 1);
    } else if (type === 'glow') {
      env = Math.sin(Math.PI * i / count) ** 2;
      v = Math.sin(p) * .68 + Math.sin(p * 2.003) * .15;
    }
    const songBeat = (begin + i) / rate / beat;
    const bar = Math.floor(songBeat / 4);
    const kickPresent = bar >= 4 && bar < 88 && !(bar >= 40 && bar < 48);
    if (kickPresent && ['pad','glow','arp'].includes(type)) {
      const phase = songBeat - Math.floor(songBeat);
      env *= .62 + .38 * Math.min(phase / .38, 1);
    }
    const l = v * env * gl, r = v * env * gr;
    left[begin + i] += l; right[begin + i] += r;
    sendL[begin + i] += l * send; sendR[begin + i] += r * send;
  }
}

for (let bar = 0; bar < bars; bar++) {
  const chord = chordAt(bar), intro = bar < 8, breakdown = bar >= 40 && bar < 48;
  const rising = bar >= 48 && bar < 56, full = bar >= 56 && bar < 80, outro = bar >= 88;
  const strength = full ? 1.08 : intro ? .78 : 1;
  if (bar % 2 === 0) {
    chord.slice(1).forEach((m, i) =>
      note(at(bar), beat * 8 + .80, m + 12, intro ? .041 : .048, 'pad', (i - 1.5) * .45, .15));
    if (bar % 8 === 0) note(at(bar, 1), beat * 7, chord[3] + 24, .021, 'glow', .35, .20);
  }

  if (bar >= 4 && !breakdown && !outro) {
    for (let q = 0; q < 4; q++) {
      if (bar < 6 && q % 2) continue;
      note(at(bar, q), .55, 0, .49 * strength, 'kick');
      if (q % 2 && bar >= 8) {
        note(at(bar, q), .26, 0, .17 * strength, 'snare', .08, .035);
        if (full) note(at(bar, q) + .009, .22, 0, .048, 'clap', -.08, .09);
      }
      note(at(bar, q + .5), q === 3 && bar % 4 === 3 ? .25 : .15,
        0, .20 * strength, 'hat', -.22, .02);
      if (!intro) note(at(bar, q + .75), .07, 0, .092, 'hat', .42);
      const passing = q === 3 && bar % 2 && bar % 8 === 7;
      const pitch = passing ? chordAt(bar + 1)[0] - 12 : chord[0] - 12;
      note(at(bar, q + .5), beat * .70, pitch, .25 * strength, 'bass');
      if (full && q === 2) note(at(bar, q + .25), beat * .20, pitch + 12, .080, 'bass');
    }
    if (bar >= 24 && !rising) {
      for (let s = 0; s < 8; s++)
        note(at(bar, s * .5 + .25), .065, 0, (s % 2 ? .065 : .038), 'shaker', s % 2 ? .28 : -.32);
    }
  }

  if (bar >= 8 && !outro) {
    const phrase = (bar - 8) % 16;
    const local = phrase % 8;
    if (phrase < 8) {
      for (const [offset, midi, held] of hook[local]) {
        if (breakdown && offset !== .5 && offset !== 2.5) continue;
        const gain = breakdown ? .080 : full ? .124 : .112;
        note(at(bar, offset), held * beat + .38, midi, gain, 'lead',
          Math.sin(local * .65) * .24, .36);
        if (full && local === 4 && offset === 3)
          note(at(bar, offset), held * beat + .65, midi - 12, .034, 'lead', -.30, .42);
      }
    } else {
      const tones = chord.slice(1);
      const steps = full ? [0,.75,1.5,2.5,3.25] : [.5,1.5,2.5,3.5];
      for (let s = 0; s < steps.length; s++) {
        const pitch = tones[(s + Math.floor(local / 2)) % tones.length] + 12;
        note(at(bar, steps[s]), .60, pitch, breakdown ? .045 : .064, 'arp',
          Math.sin(local + s * .8) * .48, .38);
      }
    }
  } else if (bar === 2 || bar === 6) {
    note(at(bar, .5), 2.2, 74, .075, 'lead', -.18, .48);
    note(at(bar, 2.5), 1.2, 77, .059, 'lead', .22, .45);
  } else if (outro && bar < 92) {
    for (const [offset, midi, held] of hook[(bar - 88) % 8])
      if (offset === .5 || offset === 2.5)
        note(at(bar, offset), held * beat + .80, midi, .065, 'lead', .12, .50);
  }

  if (bar >= 24 && bar < 80 && !breakdown && !rising) {
    const pattern = [1, 3, 2, 0];
    for (let s = 0; s < 4; s++) {
      if ((bar + s) % 3 === 0) continue;
      note(at(bar, s + .25), .36, chord[pattern[(s + bar) % 4] + 1] + 12,
        full ? .028 : .021, 'arp', s % 2 ? .58 : -.58, .30);
    }
  }
  if (breakdown) {
    note(at(bar, .5), .10, 0, .10, 'hat', -.20, .14);
    note(at(bar, 2.5), .10, 0, .085, 'hat', .20, .14);
  }
  if (rising && bar >= 52) {
    const steps = bar === 55 ? 16 : 8;
    for (let s = 0; s < steps; s++)
      note(at(bar, s * 4 / steps), .12, 0, .025 + (bar - 52) * .011 + s * .001,
        'snare', (s % 2 ? .16 : -.16), .12);
  }
  if (bar % 8 === 7 && bar >= 8 && !breakdown && !rising && !outro)
    for (let s = 0; s < 4; s++) note(at(bar, 3 + s / 4), .10, 0,
      .046 + s * .012, 'snare', (s - 1.5) * .18, .05);
}

const delayL = Math.round(beat * .75 * rate), delayR = Math.round(beat * 1.25 * rate);
let filteredL = 0, filteredR = 0;
for (let i = 0; i < frames; i++) {
  filteredL += .16 * ((i >= delayL ? sendR[i - delayL] : 0) - filteredL);
  filteredR += .16 * ((i >= delayR ? sendL[i - delayR] : 0) - filteredR);
  sendL[i] += filteredL * .36; sendR[i] += filteredR * .36;
  const fade = Math.min(i / rate / 1.1, 1) * Math.min((frames - 1 - i) / rate / 5, 1);
  left[i] = Math.tanh((left[i] + filteredL * .55) * 1.5) * fade * .90;
  right[i] = Math.tanh((right[i] + filteredR * .55) * 1.5) * fade * .90;
}

function statistics(l, r) {
  let peak = 0, sum = 0;
  for (let i = 0; i < l.length; i++) {
    if (!Number.isFinite(l[i]) || !Number.isFinite(r[i])) throw new Error('Non-finite audio');
    peak = Math.max(peak, Math.abs(l[i]), Math.abs(r[i]));
    sum += (l[i] ** 2 + r[i] ** 2) / 2;
  }
  return { peak, rms:Math.sqrt(sum / l.length) };
}
const referencePath = path.join(root, 'Assets/Liminal/Audio/TidalMemory.wav');
const reference = fs.readFileSync(referencePath);
if (reference.toString('ascii', 0, 4) !== 'RIFF' || reference.readUInt16LE(20) !== 1 ||
    reference.readUInt16LE(34) !== 16 || reference.toString('ascii',36,40) !== 'data')
  throw new Error('Unexpected reference WAV format');
let referenceEnergy = 0;
for (let i = 44; i < reference.length; i += 2) referenceEnergy += (reference.readInt16LE(i) / 32768) ** 2;
const referenceRms = Math.sqrt(referenceEnergy / ((reference.length - 44) / 2));
const raw = statistics(left, right);
const gain = Math.min(referenceRms / raw.rms, .88 / raw.peak);
for (let i = 0; i < frames; i++) { left[i] *= gain; right[i] *= gain; }
const measured = statistics(left, right);
const output = Buffer.alloc(44 + frames * 4);
output.write('RIFF',0); output.writeUInt32LE(output.length - 8,4); output.write('WAVEfmt ',8);
output.writeUInt32LE(16,16); output.writeUInt16LE(1,20); output.writeUInt16LE(2,22);
output.writeUInt32LE(rate,24); output.writeUInt32LE(rate * 4,28); output.writeUInt16LE(4,32);
output.writeUInt16LE(16,34); output.write('data',36); output.writeUInt32LE(frames * 4,40);
let clippedSamples = 0;
for (let i = 0; i < frames; i++) {
  for (const [value, offset] of [[left[i],44 + i * 4],[right[i],46 + i * 4]]) {
    if (Math.abs(value) >= 1) clippedSamples++;
    output.writeInt16LE(Math.round(Math.max(-1,Math.min(1,value)) * 32767),offset);
  }
}
events.sort((a,b) => a.sample - b.sample);
const timeline = {
  title:'Tidal Bloom / audition 01', bpm, bars, sampleRate:rate, sampleCount:frames,
  sourceSha256:createHash('sha256').update(output).digest('hex'),
  beats:Array.from({length:bars * 4},(_,i) => Math.round(i * beat * rate)),
  eighths:Array.from({length:bars * 8},(_,i) => Math.round(i * beat * rate / 2)),
  sections:sections.map(section => Math.round(at(section.bar) * rate)),
  harmony:Array.from({length:bars},(_,bar) => ({sample:Math.round(at(bar) * rate),notes:chordAt(bar)})),
  events
};
const metrics = {
  title:timeline.title, bpm, bars, duration:frames / rate, clippedSamples,
  peak:measured.peak, rms:measured.rms, referenceRms,
  relativeRmsDb:20 * Math.log10(measured.rms / referenceRms),
  sections:sections.map(s => ({...s,seconds:at(s.bar)})),
  referenceSha256:createHash('sha256').update(reference).digest('hex'),
  sourceSha256:timeline.sourceSha256,
  method:'Authored Node.js procedural synthesis. No external music generation model.',
  status:'Audition candidate; musical quality requires listener review.'
};
fs.mkdirSync(destination,{recursive:true});
fs.writeFileSync(path.join(destination,'TidalBloom.wav'),output);
fs.writeFileSync(path.join(destination,'TidalBloomTimeline.json'),JSON.stringify(timeline));
fs.writeFileSync(path.join(destination,'render.json'),JSON.stringify(metrics,null,2));
console.log(JSON.stringify(metrics));
