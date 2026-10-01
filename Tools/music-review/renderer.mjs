import fs from 'node:fs';
import path from 'node:path';

export const RATE = 44100;
export const BPM = 124;
export const BEAT = 60 / BPM;
const TAU = Math.PI * 2;
const hz = midi => 440 * 2 ** ((midi - 69) / 12);
const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));

export function readWav(filename) {
  const data = fs.readFileSync(filename);
  if (data.toString('ascii', 0, 4) !== 'RIFF' || data.toString('ascii', 8, 12) !== 'WAVE')
    throw new Error(`Not a WAV: ${filename}`);
  let format, payload;
  for (let offset = 12; offset + 8 <= data.length;) {
    const name = data.toString('ascii', offset, offset + 4);
    const size = data.readUInt32LE(offset + 4);
    if (offset + size + 8 > data.length) throw new Error('Truncated WAV');
    if (name === 'fmt ') format = data.subarray(offset + 8, offset + 8 + size);
    if (name === 'data') payload = data.subarray(offset + 8, offset + 8 + size);
    offset += 8 + size + (size % 2);
  }
  if (!format || !payload || format.readUInt16LE(0) !== 1 || format.readUInt16LE(14) !== 16)
    throw new Error(`Expected PCM16 WAV: ${filename}`);
  const channels = format.readUInt16LE(2), rate = format.readUInt32LE(4);
  const frames = payload.length / (channels * 2);
  const mono = new Float32Array(frames);
  let energy = 0, peak = 0;
  for (let i = 0; i < frames; i++) {
    for (let c = 0; c < channels; c++) mono[i] += payload.readInt16LE((i * channels + c) * 2) / (32768 * channels);
    energy += mono[i] ** 2;
    peak = Math.max(peak, Math.abs(mono[i]));
  }
  return { mono, rate, frames, rms: Math.sqrt(energy / frames), peak };
}

export function writeWav(filename, left, right) {
  const data = Buffer.alloc(44 + left.length * 4);
  data.write('RIFF', 0); data.writeUInt32LE(data.length - 8, 4); data.write('WAVEfmt ', 8);
  data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(2, 22);
  data.writeUInt32LE(RATE, 24); data.writeUInt32LE(RATE * 4, 28);
  data.writeUInt16LE(4, 32); data.writeUInt16LE(16, 34);
  data.write('data', 36); data.writeUInt32LE(left.length * 4, 40);
  let peak = 0, energy = 0, clipped = 0;
  for (let i = 0; i < left.length; i++) {
    for (let c = 0; c < 2; c++) {
      const value = c ? right[i] : left[i];
      if (!Number.isFinite(value)) throw new Error(`Non-finite audio at frame ${i}`);
      if (Math.abs(value) >= 1) clipped++;
      peak = Math.max(peak, Math.abs(value));
      energy += value ** 2;
      data.writeInt16LE(Math.round(clamp(value, -1, 1) * 32767), 44 + i * 4 + c * 2);
    }
  }
  if (clipped) throw new Error(`${clipped} clipped samples before mastering`);
  fs.writeFileSync(filename, data);
  return { peak, rms: Math.sqrt(energy / (left.length * 2)), clippedSamples: clipped };
}

// PolyBLEP limits oscillator discontinuities; filters and amplitude contours remain separate.
function blep(phase, increment) {
  if (phase < increment) { const t = phase / increment; return 2 * t - t * t - 1; }
  if (phase > 1 - increment) { const t = (phase - 1) / increment; return t * t + 2 * t + 1; }
  return 0;
}
function saw(p, dt) { return 2 * p - 1 - blep(p, dt); }
function pulse(p, dt, width) {
  return (p < width ? 1 : -1) + blep(p, dt) - blep((p - width + 1) % 1, dt);
}
function envelope(t, held, attack, release, decay = .15, sustain = .8) {
  const on = t < attack ? t / attack : sustain + (1 - sustain) * Math.exp(-(t - attack) / decay);
  return on * (t <= held ? 1 : Math.max(0, 1 - (t - held) / release) ** 1.5);
}

export class Renderer {
  constructor(spec, sampleDirectory) {
    this.spec = spec;
    this.frames = Math.round(spec.bars * 4 * BEAT * RATE);
    this.left = new Float32Array(this.frames); this.right = new Float32Array(this.frames);
    this.drumL = new Float32Array(this.frames); this.drumR = new Float32Array(this.frames);
    this.wetL = new Float32Array(this.frames); this.wetR = new Float32Array(this.frames);
    this.duck = new Float32Array(this.frames);
    this.events = []; this.harmony = []; this.sampleUsage = new Map();
    this.seed = spec.seed;
    this.samples = {};
    if (sampleDirectory) {
      const manifest = JSON.parse(fs.readFileSync(path.join(sampleDirectory, 'samples.json'), 'utf8'));
      for (const [name, entries] of Object.entries(manifest.instruments)) {
        this.samples[name] = entries.map(entry => {
          const source = readWav(path.join(sampleDirectory, entry.file));
          return { ...entry, ...source, level: Math.min(.24 / source.rms, .95 / source.peak) };
        });
      }
    }
  }
  random() { this.seed = (1664525 * this.seed + 1013904223) >>> 0; return this.seed / 4294967296; }
  time(bar, offset = 0) { return (bar * 4 + offset) * BEAT; }
  swing(offset, amount = .56) {
    const pair = Math.floor(offset * 2) / 2, phase = offset - pair;
    return pair + (phase <= .25 ? phase * amount * 2 : amount * .5 + (phase - .25) * (1 - amount) * 2);
  }
  chord(bar, notes, root, label) {
    this.harmony.push({ bar, sample: Math.round(this.time(bar) * RATE), notes, root, label });
  }
  note(bar, offset, midi, heldBeats, gain, type, options = {}) {
    const start = this.time(bar, offset) + (options.human ?? 0);
    const begin = Math.round(start * RATE), held = heldBeats * BEAT;
    const release = options.release ?? ({ keys: .32, reed: .18, brass: .15, strings: .65,
      choir: .8, bass: .065, pluck: .15, horn: .25, cello: .35, flute: .25 }[type] ?? .06);
    const length = held + release, count = Math.ceil(length * RATE);
    const f = midi === null ? 0 : hz(midi), dt = f / RATE;
    const pan = clamp(options.pan ?? 0, -1, 1);
    const gl = gain * Math.sqrt((1 - pan) / 2), gr = gain * Math.sqrt((1 + pan) / 2);
    const send = options.send ?? 0;
    const percussion = ['kick', 'heart', 'snare', 'brush', 'rim', 'hat', 'shaker', 'metal', 'ride', 'tom'].includes(type);
    const samples = this.samples[type];
    const sample = samples?.reduce((a, b) => Math.abs(b.midi - midi) < Math.abs(a.midi - midi) ? b : a);
    if (['horn', 'cello', 'flute'].includes(type) && !sample) throw new Error(`Missing acoustic ${type} samples`);
    if (sample) this.sampleUsage.set(sample.file, (this.sampleUsage.get(sample.file) ?? 0) + 1);
    const ratio = sample ? 2 ** ((midi - sample.midi) / 12) * sample.rate / RATE : 0;
    this.events.push({ sample: begin, endSample: Math.min(this.frames, begin + count), instrument: type,
      midi, gain, pan, heldBeats });
    let p = this.random(), p2 = this.random(), p3 = this.random();
    let filter1 = 0, filter2 = 0, filter3 = 0, filter4 = 0, noiseLow = 0, dc = 0;
    for (let i = 0; i < count && begin + i < this.frames; i++) {
      if (begin + i < 0) continue;
      const t = i / RATE, theta = TAU * f * t;
      let v = 0, env = 1, cutoff = 0;
      if (sample) {
        let index = i * ratio;
        if (sample.loopStart != null && sample.loopEnd > sample.loopStart && index >= sample.loopEnd)
          index = sample.loopStart + (index - sample.loopStart) % (sample.loopEnd - sample.loopStart);
        const integer = Math.floor(index), mix = index - integer;
        v = integer + 1 < sample.frames
          ? ((1 - mix) * sample.mono[integer] + mix * sample.mono[integer + 1]) * sample.level : 0;
        env = envelope(t, held, .018, release, .3, .94);
        env *= .82 + .18 * Math.sin(Math.PI * Math.min(1, t / Math.max(held, .1)));
      } else if (type === 'keys') {
        const index = .72 * Math.exp(-t * 4.2) * (gain / .06);
        v = Math.sin(theta + index * Math.sin(theta)) * .70 + Math.sin(theta * 2) * .16 * Math.exp(-t * 1.8)
          + Math.sin(theta * 3) * .08 * Math.exp(-t * 3.5) + Math.sin(theta * 7.01) * .025 * Math.exp(-t * 15);
        env = Math.min(t / .005, 1) * Math.exp(-t / (options.decay ?? 1.6));
        env *= t <= held ? 1 : Math.max(0, 1 - (t - held) / release);
        env *= .91 + .09 * Math.sin(TAU * 4.2 * t + pan * 1.5);
      } else if (type === 'reed') {
        v = pulse(p, dt, .42 + .035 * Math.sin(t * 2.7)) * .38 + Math.sin(theta) * .62;
        env = envelope(t, held, .026, release, .23, .72);
        cutoff = (options.brightness ?? 1500) + 1100 * Math.exp(-t * 5);
      } else if (type === 'brass') {
        v = saw(p, dt) * .45 + saw(p2, dt * 1.003) * .35 + Math.sin(theta * .5) * .20;
        env = envelope(t, held, .024, release, .13, .76);
        cutoff = (options.brightness ?? 1100) + 2900 * Math.exp(-t * 6) + 450 * Math.min(t / .6, 1);
      } else if (type === 'strings') {
        v = saw(p, dt) * .35 + saw(p2, dt * 1.004) * .33 + saw(p3, dt * .996) * .32;
        env = envelope(t, held, options.attack ?? .38, release, .6, .92);
        cutoff = options.brightness ?? 1900;
      } else if (type === 'choir') {
        for (let h = 1; h <= 10; h++) {
          const partial = f * h;
          const formant = .8 * Math.exp(-(((partial - 650) / 180) ** 2))
            + .45 * Math.exp(-(((partial - 1100) / 230) ** 2)) + .2 * Math.exp(-(((partial - 2400) / 450) ** 2));
          v += Math.sin(theta * h + .02 * Math.sin(t * 2.4)) * (formant + .08) / Math.sqrt(h);
        }
        env = envelope(t, held, .65, release, .3, .9);
        v *= .8;
      } else if (type === 'bass') {
        const electric = options.flavour === 'electric';
        v = electric ? Math.sin(theta) * .70 + Math.sin(theta * 2) * .22 + Math.sin(theta * 3) * .08
          : pulse(p, dt, .5) * .55 + Math.sin(theta) * .45;
        env = envelope(t, held, .006, release, .14, electric ? .45 : .66);
        cutoff = electric ? 900 : 400 + 1400 * Math.exp(-t * 11);
      } else if (type === 'pluck') {
        v = pulse(p, dt, .32) * .40 + Math.sin(theta) * .60;
        env = Math.min(t / .003, 1) * Math.exp(-t * 9) * Math.max(0, 1 - Math.max(0, t - held) / release);
        cutoff = 900 + 2800 * Math.exp(-t * 15);
      } else if (type === 'kick' || type === 'heart') {
        const heavy = type === 'kick';
        const base = heavy ? 47 : 55, sweep = heavy ? 2.3 : .95;
        v = Math.sin(TAU * (base * t + sweep * (1 - Math.exp(-t * 52))))
          + (this.random() * 2 - 1) * Math.exp(-t * 250) * .13;
        env = Math.min(t / .001, 1) * Math.exp(-t * (heavy ? 10 : 7));
        this.duck[begin + i] = Math.max(this.duck[begin + i], Math.exp(-t * 16));
      } else if (type === 'snare' || type === 'brush' || type === 'rim') {
        const noise = this.random() * 2 - 1;
        noiseLow += .16 * (noise - noiseLow);
        if (type === 'rim') {
          v = Math.sin(TAU * 830 * t) * .48 * Math.exp(-t * 70)
            + Math.sin(TAU * 1570 * t) * .25 * Math.exp(-t * 85) + (noise - noiseLow) * .16;
          env = Math.min(t / .0007, 1) * Math.exp(-t * 45);
        } else if (type === 'brush') {
          v = noiseLow * 2 + Math.sin(TAU * 170 * t) * .09 * Math.exp(-t * 30);
          env = Math.min(t / .012, 1) * Math.exp(-t * 13);
        } else {
          v = (noise - noiseLow) * .82 + Math.sin(TAU * 186 * t) * .25 * Math.exp(-t * 26);
          env = Math.min(t / .001, 1) * Math.exp(-t * 19);
        }
      } else if (type === 'hat' || type === 'shaker' || type === 'metal' || type === 'ride') {
        const noise = this.random() * 2 - 1;
        noiseLow += .64 * (noise - noiseLow);
        const metallic = Math.sin(TAU * 3180 * t) * Math.sin(TAU * 4637 * t)
          + Math.sin(TAU * 5871 * t) * .3;
        v = type === 'metal' || type === 'ride' ? metallic * .28 + (noise - noiseLow) * .65
          : noise - noiseLow;
        env = Math.min(t / .001, 1) * Math.exp(-t * ({ hat: 80, shaker: 60, metal: 24, ride: 8 }[type]));
      } else if (type === 'tom') {
        v = Math.sin(TAU * (f * t + .25 * (1 - Math.exp(-t * 25)))) * .90
          + (this.random() * 2 - 1) * .13 * Math.exp(-t * 100);
        env = Math.min(t / .001, 1) * Math.exp(-t * 13);
      } else throw new Error(`Unknown instrument ${type}`);
      if (cutoff) {
        const coefficient = 1 - Math.exp(-TAU * Math.min(cutoff, 13000) / RATE);
        filter1 += coefficient * (v - filter1); filter2 += coefficient * (filter1 - filter2);
        filter3 += coefficient * (filter2 - filter3); filter4 += coefficient * (filter3 - filter4);
        v = type === 'reed' || type === 'strings' ? filter2 : filter4;
      }
      dc += .001 * (v - dc);
      v = (v - dc) * env;
      const frame = begin + i;
      this.left[frame] += v * gl; this.right[frame] += v * gr;
      if (percussion) { this.drumL[frame] += v * gl; this.drumR[frame] += v * gr; }
      this.wetL[frame] += v * gl * send; this.wetR[frame] += v * gr * send;
      const vibrato = (type === 'brass' || type === 'reed') ? .0013 * Math.min(t / .4, 1) * Math.sin(TAU * 5.1 * t) : 0;
      p = (p + dt * (1 + vibrato)) % 1;
      p2 = (p2 + dt * (1.003 + .0008 * Math.sin(t * 1.7))) % 1;
      p3 = (p3 + dt * (.997 + .0009 * Math.sin(t * 2.1 + 2))) % 1;
    }
  }
  finish(filename) {
    const sizeL = [.0297, .0371, .0411, .0437].map(n => Math.round(n * RATE));
    const sizeR = [.0305, .0383, .0421, .0451].map(n => Math.round(n * RATE));
    const combL = sizeL.map(n => new Float32Array(n)), combR = sizeR.map(n => new Float32Array(n));
    const lowL = [0, 0, 0, 0], lowR = [0, 0, 0, 0];
    const delayL = Math.round(BEAT * (this.spec.delayBeats ?? .75) * RATE);
    const delayR = delayL + Math.round(.014 * RATE);
    const feedback = this.spec.roomFeedback ?? .76, echo = this.spec.echo ?? .12;
    let highpassL = 0, highpassR = 0, peak = 0;
    for (let i = 0; i < this.frames; i++) {
      const echoL = i >= delayL ? this.wetR[i - delayL] * echo : 0;
      const echoR = i >= delayR ? this.wetL[i - delayR] * echo : 0;
      this.wetL[i] += echoL * .5; this.wetR[i] += echoR * .5;
      highpassL += .022 * (this.wetL[i] - highpassL);
      highpassR += .022 * (this.wetR[i] - highpassR);
      const inputL = this.wetL[i] - highpassL, inputR = this.wetR[i] - highpassR;
      let roomL = 0, roomR = 0;
      for (let c = 0; c < 4; c++) {
        const il = i % sizeL[c], ir = i % sizeR[c];
        lowL[c] += .35 * (combL[c][il] - lowL[c]);
        lowR[c] += .35 * (combR[c][ir] - lowR[c]);
        roomL += combL[c][il] * .25; roomR += combR[c][ir] * .25;
        combL[c][il] = inputL + lowL[c] * feedback;
        combR[c][ir] = inputR + lowR[c] * feedback;
      }
      const pump = 1 - this.duck[i] * (this.spec.duck ?? .08);
      const fade = Math.min(i / (RATE * .018), 1) * Math.min((this.frames - 1 - i) / (RATE * 2.1), 1);
      this.left[i] = Math.tanh(((this.left[i] - this.drumL[i]) * pump + this.drumL[i] + roomL * .6 + echoL) * 1.12) * fade;
      this.right[i] = Math.tanh(((this.right[i] - this.drumR[i]) * pump + this.drumR[i] + roomR * .6 + echoR) * 1.12) * fade;
      peak = Math.max(peak, Math.abs(this.left[i]), Math.abs(this.right[i]));
    }
    const gain = .82 / peak;
    for (let i = 0; i < this.frames; i++) { this.left[i] *= gain; this.right[i] *= gain; }
    return writeWav(filename, this.left, this.right);
  }
}
