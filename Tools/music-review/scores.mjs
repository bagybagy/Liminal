export const scores = [
  {
    id: 'LanternCurrent', title: 'Lantern Current', bars: 80, seed: 10262101,
    character: 'Warm broken-beat soul: electric keys, expressive reed, syncopated round bass.',
    roomFeedback: .71, echo: .14, duck: .07,
    sections: [
      [0, 'Keys and bass invitation'], [4, 'Broken-beat enters'], [8, 'Reed theme'],
      [24, 'Two-voice conversation'], [32, 'Bass and keys pocket'], [40, 'Harmonic bridge'],
      [48, 'Open-water refrain'], [64, 'Close-up reprise'], [72, 'Warm resolution']
    ], compose: lantern
  },
  {
    id: 'ScarletVector', title: 'Scarlet Vector', bars: 96, seed: 10262102,
    character: 'Melodic electro engine: analogue brass, square bass, tom machinery, straight pulse.',
    roomFeedback: .66, echo: .095, duck: .16, delayBeats: .5,
    sections: [
      [0, 'Motor ignition'], [8, 'Brass insignia'], [24, 'Interlocking machinery'],
      [40, 'Suspended engine'], [48, 'Acceleration'], [64, 'Full power'],
      [80, 'Bright release'], [88, 'Engine cooling']
    ], compose: scarlet
  },
  {
    id: 'HorizonCanticle', title: 'Horizon Canticle', bars: 88, seed: 10262103,
    character: 'Broad half-time ocean hymn: recorded cello and horn, ensemble strings, flute replies.',
    roomFeedback: .84, echo: .055, duck: .045, delayBeats: 1.5, acoustic: true,
    sections: [
      [0, 'Solo cello breath'], [4, 'Horn answers'], [8, 'Wide horizon'],
      [24, 'Flute and cello dialogue'], [40, 'Surface light'], [56, 'Great ascent'],
      [72, 'Distant shore'], [80, 'Last breath']
    ], compose: horizon
  }
];

const chord = (root, notes, label) => ({ root, notes, label });
const warmHarmony = [
  chord(38, [53, 57, 60, 64, 69], 'Dm9'), chord(38, [53, 57, 60, 64, 69], 'Dm9'),
  chord(43, [53, 57, 59, 64, 69], 'G13'), chord(43, [53, 57, 59, 64, 69], 'G13'),
  chord(36, [52, 55, 59, 62, 67], 'Cmaj9'), chord(41, [52, 57, 60, 64, 67], 'Fmaj9'),
  chord(41, [52, 57, 60, 64, 69], 'Fmaj9'), chord(33, [55, 57, 61, 64, 67], 'A7')
];
const warmBridge = [
  chord(34, [53, 57, 60, 62, 65], 'Bbmaj9'), chord(34, [53, 57, 60, 62, 65], 'Bbmaj9'),
  chord(41, [52, 57, 60, 64, 67], 'Fmaj9'), chord(41, [52, 57, 60, 64, 67], 'Fmaj9'),
  chord(43, [53, 57, 58, 62, 65], 'Gm9'), chord(43, [53, 57, 58, 62, 65], 'Gm9'),
  chord(33, [55, 57, 62, 64, 67], 'A7sus4'), chord(33, [55, 57, 61, 64, 67], 'A7')
];
// Each theme is authored as a phrase, not selected randomly from the current chord.
const warmTheme = [
  [[.25,62,1.4],[2,65,.5],[2.75,64,.4],[3.25,62,.6]],
  [[0,69,1.1],[1.5,67,.65],[2.5,65,.8]],
  [[.25,67,1.6],[2.5,64,.6],[3.25,62,.55]],
  [[.5,64,1],[2,67,1],[3.25,69,.5]],
  [[0,64,.75],[1.25,67,.65],[2,72,1],[3.25,71,.5]],
  [[0,69,1.5],[2,67,.65],[3,65,.75]],
  [[.25,64,1.25],[1.75,65,.5],[2.5,69,1]],
  [[0,67,.65],[1,64,.6],[2,61,.6],[3,62,.85]]
];
const warmAnswer = [
  [[.5,65,.5],[1.25,64,.5],[2.5,62,1]],
  [[0,60,.8],[1.5,62,.6],[3,65,.75]],
  [[.5,59,.65],[1.5,62,.6],[2.5,64,.75]],
  [[0,67,.8],[1.5,64,.65],[3,62,.75]],
  [[.25,64,1],[1.75,62,.6],[3,59,.75]],
  [[.5,60,1.5],[2.5,64,1]],
  [[0,65,1],[1.5,64,.65],[3,60,.7]],
  [[.5,61,.65],[1.5,64,.7],[3,62,.8]]
];

function lantern(r) {
  for (let bar = 0; bar < 80; bar++) {
    const bridge = bar >= 40 && bar < 48, full = bar >= 48 && bar < 64;
    const close = bar >= 64 && bar < 72, end = bar >= 72, local = bar % 8;
    const h = bridge ? warmBridge[local] : warmHarmony[local];
    r.chord(bar, h.notes, h.root, h.label);
    const voicing = close ? h.notes.slice(1, 4) : h.notes;
    const strikes = bar < 4 || bridge || end ? [[0,2.6,.056],[3,.6,.036]]
      : [[.0,.85,.059],[1.75,.60,.043],[2.75,.90,.048]];
    for (const [offset, held, gain] of strikes) {
      if (end && bar >= 76 && offset !== 0) continue;
      voicing.forEach((midi, i) => r.note(bar, r.swing(offset), midi, held,
        gain * (full ? 1.12 : 1), 'keys', { pan: (i - 2) * .18, send: .24,
          human: i * .0022, release: .33, decay: end ? 2.2 : 1.5 }));
    }
    const bass = end ? [[0,2.5,0],[3,.7,7]] : bridge ? [[0,1.3,0],[2.5,1.2,7]]
      : [[0,.65,0],[.75,.50,0],[1.75,.55,7],[2.5,.60,0],[3.5,.30,12]];
    if (bar < 78) for (const [offset, held, interval] of bass)
      r.note(bar, r.swing(offset), h.root + interval, held, .27, 'bass', { flavour: 'electric' });

    if (bar < 4) {
      r.note(bar, 1, null, .24, .13, 'rim', { pan: -.1, send: .08 });
      r.note(bar, 3, null, .24, .16, 'rim', { pan: -.1, send: .08 });
    } else if (!end) {
      const kicks = bridge ? [0,2.5] : bar % 2 ? [0,1.75,2.5,3.5] : [0,1.5,2.75];
      for (const q of kicks) r.note(bar, r.swing(q), null, 1.05, full ? .49 : .45, 'kick');
      for (const q of [1,3]) {
        r.note(bar, q + .009 / (60 / 124), null, .55, bridge ? .13 : .22, 'brush', { send: .08, pan: .08 });
        r.note(bar, q, null, .18, .17, 'rim', { pan: -.09, send: .06 });
      }
      const hats = bridge ? [0.5,1.5,2.5,3.5] : [0,.5,1,1.5,2,2.5,3,3.5];
      for (let i = 0; i < hats.length; i++) r.note(bar, hats[i], null, .18,
        i % 2 ? .13 : .078, 'hat', { pan: -.28 });
      if (bar >= 16 && !bridge) for (let s = 0; s < 16; s++) {
        if (s % 4 === 0 || s === 12) continue;
        r.note(bar, r.swing(s / 4), null, .15, s % 2 ? .055 : .032, 'shaker', { pan: .38 });
      }
      if (bar % 8 === 7 && !bridge) {
        r.note(bar, r.swing(3.25), null, .17, .068, 'brush', { pan: .17 });
        r.note(bar, r.swing(3.75), null, .17, .092, 'brush', { pan: -.17 });
      }
    }

    if (bar >= 8 && bar < 24 || full || close) {
      for (const [offset, midi, held] of warmTheme[local]) {
        if (close && offset > 2.5) continue;
        r.note(bar, r.swing(offset), midi, held, full ? .24 : .21, 'reed',
          { pan: -.045, send: .26, brightness: full ? 1900 : 1450 });
      }
      if (full && local % 2) for (const [offset, midi, held] of warmAnswer[local])
        if (offset > 2) r.note(bar, r.swing(offset), midi - 12, held, .048, 'pluck', { pan: .48, send: .15 });
    } else if (bar >= 24 && bar < 32) {
      warmAnswer[local].forEach(([offset, midi, held], i) => r.note(bar, r.swing(offset), midi, held,
        i % 2 ? .18 : .19, i % 2 ? 'keys' : 'reed', { pan: i % 2 ? .22 : -.16, send: .25 }));
    } else if (bar >= 32 && bar < 40) {
      for (const [offset, midi, held] of warmAnswer[local])
        r.note(bar, r.swing(offset), midi, held, .13, 'keys', { pan: .18, send: .17 });
    } else if (bridge) {
      const melody = [[65,64],[60,57],[64,60],[57,55],[62,65],[69,65],[64,62],[61,64]][local];
      melody.forEach((midi, i) => r.note(bar, i * 2, midi, 1.55, .18, 'reed', { send: .3 }));
    } else if (bar === 4 || bar === 6) {
      for (const [offset, midi, held] of warmTheme[0].slice(0, 2))
        r.note(bar, offset, midi, held, .16, 'reed', { send: .23 });
    } else if (end && bar < 76) {
      for (const [offset, midi, held] of warmAnswer[local].slice(0, 2))
        r.note(bar, offset, midi, held * 1.3, .14, 'keys', { send: .3, decay: 2 });
    }
  }
}

const motorHarmony = [
  chord(38,[50,57,62,65],'Dm'), chord(38,[50,57,62,65],'Dm'),
  chord(34,[53,58,62,65],'Bbmaj7'), chord(34,[53,58,62,65],'Bbmaj7'),
  chord(43,[50,58,62,67],'Gm'), chord(43,[50,58,62,67],'Gm'),
  chord(33,[52,57,61,67],'A7'), chord(33,[52,57,61,67],'A7')
];
const brightMotor = [
  chord(41,[53,57,60,65],'F'), chord(41,[53,57,60,65],'F'),
  chord(36,[52,55,60,64],'C'), chord(36,[52,55,60,64],'C'),
  chord(34,[53,58,62,65],'Bb'), chord(34,[53,58,62,65],'Bb'),
  chord(33,[52,57,61,67],'A7'), chord(38,[50,57,62,65],'Dm')
];
const motorTheme = [
  [[0,62,.45],[.75,62,.35],[1.5,65,.60],[2.5,67,.65],[3.5,65,.35]],
  [[0,69,.85],[1.25,67,.50],[2,65,.65],[3,62,.65]],
  [[0,70,.85],[1.25,69,.60],[2.25,65,.65],[3.25,62,.40]],
  [[0,65,.55],[1,62,.65],[2.25,65,.50],[3,69,.65]],
  [[0,67,.80],[1.25,69,.45],[2,74,.90],[3.25,72,.45]],
  [[0,70,.70],[1,69,.65],[2,67,.65],[3,62,.70]],
  [[0,64,.85],[1.5,67,.50],[2.5,69,.50],[3.25,67,.40]],
  [[0,64,.65],[1,61,.65],[2,64,.50],[3,62,.80]]
];

function scarlet(r) {
  for (let bar = 0; bar < 96; bar++) {
    const suspended = bar >= 40 && bar < 48, full = bar >= 64 && bar < 80;
    const release = bar >= 80, end = bar >= 88, local = bar % 8;
    const h = release && !end ? brightMotor[local] : motorHarmony[local];
    r.chord(bar, h.notes, h.root, h.label);
    const ignition = bar < 8;
    const notes = suspended ? [[0,2.8,0],[3,.65,7]] : [
      [0,.35,0],[.5,.32,0],[.75,.24,12],[1.25,.32,0],[1.75,.24,7],
      [2,.35,0],[2.5,.32,12],[3,.32,0],[3.5,.32,7]
    ];
    if (bar < 94) for (let i = 0; i < notes.length; i++) {
      const [offset, held, step] = notes[i];
      if (end && i % 2) continue;
      r.note(bar, offset, h.root + step, held, .29, 'bass', { release: suspended ? .25 : .055 });
    }
    const chordBeats = suspended ? [0] : ignition ? [0,2.5] : [0,1.75,3.25];
    for (const offset of chordBeats) h.notes.slice(0, 3).forEach((midi, i) =>
      r.note(bar, offset, midi, suspended ? 3.3 : .38, suspended ? .052 : .047, 'brass',
        { pan: (i - 1) * .33, brightness: ignition ? 700 + bar * 70 : full ? 2200 : 950, send: .11 }));
    if (!end) {
      const kicks = suspended ? [0] : ignition && bar < 4 ? [0,2] : [0,1,2,3];
      for (const q of kicks) r.note(bar, q, null, 1.1, full ? .56 : .49, 'kick');
      if (!ignition && !suspended) for (const q of [1,3])
        r.note(bar, q, null, .52, full ? .25 : .21, 'snare', { pan: .04, send: .065 });
      for (const q of suspended ? [.5,2.5] : [.5,1.5,2.5,3.5])
        r.note(bar, q, null, .20, suspended ? .08 : .18, 'hat', { pan: -.31 });
      if (bar >= 16 && !suspended) for (const q of [.25,1.25,2.75,3.75])
        r.note(bar, q, null, .2, full ? .085 : .055, 'metal', { pan: .42, send: .035 });
      if (bar >= 24 && !suspended) for (const [offset, midi, gain] of [[.75,43,.095],[1.5,38,.073],[2.75,41,.12],[3.75,38,.065]])
        r.note(bar, offset, midi, .47, gain, 'tom', { pan: Math.sin(offset * 2) * .38, send: .065 });
      if (bar % 8 === 7 && bar >= 8 && !suspended) for (let s = 0; s < 3; s++)
        r.note(bar, 3 + s / 3, [50,45,38][s], .45, .11 + s * .015, 'tom', { pan: (s - 1) * .34, send: .08 });
    }
    const statement = bar >= 8 && bar < 24 || bar >= 48 && bar < 80;
    if (statement) {
      for (const [offset, midi, held] of motorTheme[local]) {
        r.note(bar, offset, midi, held, full ? .23 : .20, 'brass',
          { brightness: full ? 2300 : 1250 + Math.max(0, bar - 48) * 30, send: .20, pan: -.035 });
        if (full && held > .65) r.note(bar, offset, midi - 12, held, .041, 'brass',
          { brightness: 1300, send: .18, pan: .25 });
      }
    } else if (bar >= 24 && bar < 40) {
      for (let s = 0; s < 4; s++) {
        const midi = h.notes[[0,2,1,2][s]] + (s === 3 ? 12 : 0);
        r.note(bar, [0,.75,2,2.75][s], midi, .45, s % 2 ? .12 : .13,
          s % 2 ? 'brass' : 'pluck', { pan: s % 2 ? .24 : -.24, send: .12, brightness: 1700 });
      }
    } else if (suspended) {
      const [offset, midi] = motorTheme[local][0];
      r.note(bar, offset, midi - 12, 2.5, .17, 'brass', { brightness: 1200, release: .5, send: .31 });
      if (bar === 47) r.note(bar, 3, 61, .85, .16, 'brass', { brightness: 2800, send: .12 });
    } else if (release && !end) {
      const descending = [69,67,64,62,65,62,61,62];
      r.note(bar, 0, descending[local], 2.3, .18, 'brass', { brightness: 2100, send: .21, release: .4 });
      r.note(bar, 3, h.notes[2] + 12, .6, .09, 'pluck', { send: .16, pan: .36 });
    } else if (end && bar % 2 === 0) {
      r.note(bar, 0, h.notes[2], 2.8, .10, 'brass', { brightness: 950, release: .4, send: .28 });
    }
  }
}

const seaHarmony = [
  chord(38,[50,57,64,65,69],'Dm(add9)'), chord(34,[53,57,62,65,69],'Bbmaj7'),
  chord(41,[53,57,60,64,69],'Fmaj9'), chord(36,[52,55,60,62,67],'Cadd9'),
  chord(43,[50,57,58,62,67],'Gm9'), chord(34,[53,58,60,62,65],'Bbadd9'),
  chord(33,[52,57,61,64,67],'A7'), chord(38,[50,57,59,62,65],'Dm6')
];
const seaLight = [
  chord(41,[53,57,60,64,69],'Fmaj9'), chord(43,[53,58,62,65,69],'Gm9'),
  chord(45,[55,60,64,67,69],'Am7'), chord(34,[53,57,62,65,69],'Bbmaj7'),
  chord(36,[53,57,60,65,69],'F/C'), chord(36,[55,60,62,65,67],'Csus2'),
  chord(33,[52,57,61,64,67],'A7'), chord(38,[53,57,60,64,69],'Dm9')
];
const seaTheme = [
  [[0,62,2.7],[3,65,.85]],
  [[0,65,2],[2.5,64,.70],[3.5,62,.42]],
  [[0,69,2.6],[3,67,.85]],
  [[0,64,3.4]],
  [[0,62,1.8],[2,67,1.8]],
  [[0,70,2.3],[2.5,69,.65],[3.5,65,.42]],
  [[0,64,2],[2.5,61,1.25]],
  [[0,62,3.6]]
];
const seaResponse = [
  [[.25,69,2],[2.75,65,1]],
  [[0,74,1.5],[2,72,.8],[3,69,.8]],
  [[0,72,2.5],[3,69,.8]],
  [[0,67,1.6],[2,64,1.6]],
  [[0,67,2],[2.5,69,1]],
  [[0,74,2.8],[3,72,.7]],
  [[0,73,1.8],[2,71,.8],[3,69,.8]],
  [[0,69,1.8],[2,65,1.8]]
];

function horizon(r) {
  for (let bar = 0; bar < 88; bar++) {
    const local = bar % 8, light = bar >= 40 && bar < 56, full = bar >= 56 && bar < 72;
    const retreat = bar >= 72, end = bar >= 80;
    const h = light ? seaLight[local] : seaHarmony[local];
    r.chord(bar, h.notes, h.root, h.label);
    if (bar < 4) {
      for (const [offset, midi, held] of seaTheme[local])
        r.note(bar, offset, midi - 12, held, .48, 'cello', { pan: -.14, send: .39 });
      continue;
    }
    if (!end) {
      const texture = bar < 8 ? .022 : full ? .060 : light ? .047 : retreat ? .032 : .039;
      h.notes.forEach((midi, i) => r.note(bar, .0, midi, 3.9, texture, 'strings',
        { pan: (i - 2) * .31, send: .45, attack: full ? .18 : .43, brightness: full ? 3000 : 1600,
          release: .52 }));
      if (full) h.notes.slice(2).forEach((midi, i) => r.note(bar, .25, midi, 3.2, .029, 'choir',
        { pan: (i - 1) * .38, send: .5 }));
      r.note(bar, 0, h.root, 2.7, bar < 8 ? .13 : .20, 'bass', { flavour: 'electric', release: .3 });
      r.note(bar, 0, h.root + 12, 3.3, full ? .17 : .105, 'cello', { pan: -.26, send: .30 });
    }
    if (bar >= 8 && !end) {
      const q = full ? [0,1.5,3.25] : retreat ? [0] : [0,1.5];
      for (const offset of q) r.note(bar, offset, null, 1.1, full ? .45 : .34, 'heart');
      r.note(bar, 2, null, .85, full ? .23 : .17, 'brush', { send: .18, pan: .09 });
      if (!retreat) for (const offset of [.5,1.5,2.5,3.5])
        r.note(bar, offset, null, .28, full ? .095 : .064, 'shaker', { pan: .33 });
      if (full) for (const offset of [0,2])
        r.note(bar, offset, null, 1.3, .065, 'ride', { pan: -.40, send: .2 });
      if (bar % 8 === 7 && !retreat) for (const [offset, pitch] of [[3,45],[3.5,38]])
        r.note(bar, offset, pitch, .6, .12, 'tom', { send: .25, pan: offset === 3 ? -.35 : .35 });
    }
    if (bar >= 4 && bar < 24 || full) {
      for (const [offset, midi, held] of seaTheme[local]) {
        r.note(bar, offset, midi, held, full ? .65 : .56, 'horn', { pan: .075, send: .34 });
        if (full && held > 1.5) r.note(bar, offset, midi - 12, held, .14, 'cello', { pan: -.22, send: .33 });
      }
    } else if (bar >= 24 && bar < 40) {
      const solo = bar < 32 ? 'flute' : 'cello';
      for (const [offset, midi, held] of seaResponse[local])
        r.note(bar, offset, solo === 'cello' ? midi - 24 : midi, held, solo === 'cello' ? .51 : .48, solo,
          { pan: solo === 'cello' ? -.15 : .15, send: .40 });
    } else if (light) {
      const lift = [69,70,72,74,72,67,64,65][local];
      r.note(bar, 0, lift, 2.65, .54, 'horn', { send: .36, pan: .075 });
      const answer = [65,67,69,70,69,67,61,62][local];
      r.note(bar, 3, answer, .8, .38, 'flute', { pan: -.21, send: .40 });
    } else if (retreat) {
      for (const [offset, midi, held] of seaTheme[local]) {
        if (end && offset > 0) continue;
        r.note(bar, offset, midi - 12, held, end ? .38 : .43, 'cello', { pan: -.08, send: .44 });
      }
      if (end && bar % 2 === 0) for (const [i, midi] of h.notes.slice(1, 4).entries())
        r.note(bar, .25, midi, 3.4, .028, 'strings', { pan: (i - 1) * .3, send: .45, attack: .6 });
    }
  }
}
