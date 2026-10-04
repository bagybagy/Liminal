# Recorded SE audition sample subset

Source: the official [VSCO-2-CE repository pinned at 6dd651d55dde97fd4028699be9d4481f26917891](https://github.com/sgossner/VSCO-2-CE/tree/6dd651d55dde97fd4028699be9d4481f26917891), maintained by Versilian Studios / Sam Gossner.

License: CC0-1.0, confirmed from the pinned [official LICENSE](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/LICENSE). The [official Versilian Studios library page](https://versilian-studios.com/vsco-community/) also identifies the Community Edition and original WAV recordings as CC0 / public domain. Courtesy credits: recordings by Sam Gossner and Simon Dalzell; sample cutting by Elan Hickler / Soundemote, as recorded in the existing `Tools/music-samples/SOURCES.md`.

Only six individual official SFZ mappings and twelve individual mapped WAVs were fetched, not an archive or full library. Downloaded mapping files are retained in `_sfz/`. MIDI roots were parsed from each region's `pitch_keycenter`, with control/global/group inheritance; filenames were not used to infer pitch. One region per selected root was chosen from the lowest available velocity layer and first mapped round robin. Roots are nearest to MIDI 60 and 72 in each official mapping.

## Identity and layer choices

| Manifest identity | Actual recorded instrument | Roots | Selected dynamics / limitation |
|---|---|---|---|
| `piano` | Upright Piano Nr1 | 60, 72 | Pianissimo (`pp`), SFZ velocity 0-60, RR1. |
| `bell_keys` | Glockenspiel | 67, 72 | Celesta is absent in the pinned tree. Glockenspiel has one medium layer, velocity 0-127; its lowest mapped root is 67. |
| `marimba` | Marimba | 59, 72 | Only one mapped layer, velocity 0-127, with recordings labelled `loud`; no softer mapped recording is available. |
| `vibraphone` | Tubular Bells | 60, 72 | Vibraphone is absent in the pinned tree. Tubular bells are a distinct recorded struck-metal substitute, not a vibraphone recording. One mapped layer (`v4`), velocity 0-127; its acoustic dynamic is not specified by the SFZ. |
| `harp` | Harp | 59, 72 | Mezzo-forte (`mf`), the sole mapped layer, velocity 0-127. |
| `pizzicato` | Solo Violin Pizzicato | 60, 72 | Piano (`p`), SFZ velocity 0-62, RR1. |

## Exact WAV sources and mapped regions

Region text below preserves all original opcodes, with multiline whitespace collapsed for readability. Line numbers refer to the opening `<region>` in the pinned SFZ. `lovel` and `hivel` give the original velocity range. The original SFZ `volume` opcodes are documented but are not baked into the converted recordings.

### Piano: [VSUpright1.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/VSUpright1.sfz)

- `piano-60.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Keys/Upright%20Nr1/UR1_C4_pp_RR1.wav), region line 73: `<region> sample=UR1_C4_pp_RR1.wav lokey=58 hikey=63 pitch_keycenter=60 lovel=0 hivel=60 volume=32`
- `piano-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Keys/Upright%20Nr1/UR1_C5_pp_RR1.wav), region line 83: `<region> sample=UR1_C5_pp_RR1.wav lokey=70 hikey=75 pitch_keycenter=72 lovel=0 hivel=60 volume=32`

### Bell keys: [Glockenspiel.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Glockenspiel.sfz)

- `bell_keys-67.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/Glock/glock_medium_G4.wav), region line 63: `<region> sample=glock_medium_G4.wav lokey=67 hikey=69 pitch_keycenter=67 lovel=0 hivel=127 volume=8`
- `bell_keys-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/Glock/glock_medium_C5.wav), region line 33: `<region> sample=glock_medium_C5.wav lokey=70 hikey=75 pitch_keycenter=72 lovel=0 hivel=127 volume=8`

### Marimba: [Marimba.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Marimba.sfz)

- `marimba-59.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/Marimba/Marimba_hit_Outrigger_B2_loud_01.wav), region line 33: `<region> sample=Marimba_hit_Outrigger_B2_loud_01.wav lokey=57 hikey=61 pitch_keycenter=59 lovel=0 hivel=127 volume=6`
- `marimba-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/Marimba/Marimba_hit_Outrigger_C4_loud_01.wav), region line 63: `<region> sample=Marimba_hit_Outrigger_C4_loud_01.wav lokey=69 hikey=75 pitch_keycenter=72 lovel=0 hivel=127 volume=6`

### Vibraphone substitute: [TubularBells.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/TubularBells.sfz)

- `vibraphone-60.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/TB_hit_C4_v4_rr1.wav), region line 56: `<region> sample=TB_hit_C4_v4_rr1.wav lokey=60 hikey=63 pitch_keycenter=60 lovel=0 hivel=127 volume=6`
- `vibraphone-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Percussion/TB_hit_C5_v4_rr1.wav), region line 66: `<region> sample=TB_hit_C5_v4_rr1.wav lokey=70 hikey=74 pitch_keycenter=72 lovel=0 hivel=127 volume=6`

### Harp: [Harp.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Harp.sfz)

- `harp-59.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Harp/KSHarp_B3_mf.wav), region line 83: `<region> sample=KSHarp_B3_mf.wav lokey=57 hikey=60 pitch_keycenter=59 lovel=0 hivel=127 volume=10`
- `harp-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Harp/KSHarp_C5_mf.wav), region line 123: `<region> sample=KSHarp_C5_mf.wav lokey=71 hikey=73 pitch_keycenter=72 lovel=0 hivel=127 volume=10`

### Pizzicato: [SViolinPizz.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/SViolinPizz.sfz)

- `pizzicato-60.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Solo%20Violin/Pizz/LLVln_Pizz_C4_p_RR1.wav), region line 73: `<region> sample=LLVln_Pizz_C4_p_RR1.wav lokey=59 hikey=61 pitch_keycenter=60 lovel=0 hivel=62 volume=21`
- `pizzicato-72.wav`: [exact source WAV](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Solo%20Violin/Pizz/LLVln_Pizz_C5_p_RR1.wav), region line 83: `<region> sample=LLVln_Pizz_C5_p_RR1.wav lokey=71 hikey=73 pitch_keycenter=72 lovel=0 hivel=62 volume=21`

## Conversion

Installed FFmpeg converts each complete source file with `-ac 1 -ar 44100 -c:a pcm_s16le -map_metadata -1`. There is no time limit, trimming, tail fade, normalization, SFZ gain application, or pitch shift. The natural recorded decay and any original trailing silence remain. Preview/export code can set its own audition gains; choosing a low recorded velocity layer changes timbre, while lowering playback volume alone does not.

## Completed numeric verification

All twelve manifest WAVs passed FFprobe format checks: mono, 44100 Hz, signed PCM16, positive duration. Full-file decoding and float-converted FFmpeg `astats` confirmed nonzero sample counts, finite nonzero peaks, and zero NaNs / infinities. Every converted duration matched its downloaded source within 1 ms, retaining the complete tail. Each manifest root and exact source URL matched its parsed SFZ region and the first region of the lowest available velocity layer. The two selected roots are nearest to 60 and 72 in each mapping; using the nearest selected root for MIDI 62-74 requires at most six semitones of playback transposition (five for glockenspiel).

| Identity | Lower-root duration / peak dBFS | Upper-root duration / peak dBFS |
|---|---|---|
| `piano` | 60: 11.015147 s / -32.433763 | 72: 8.893401 s / -36.364412 |
| `bell_keys` | 67: 7.206712 s / -28.440565 | 72: 7.206712 s / -28.280932 |
| `marimba` | 59: 5.992948 s / -30.493345 | 72: 3.841723 s / -23.893614 |
| `vibraphone` (tubular bells) | 60: 19.144444 s / -12.889222 | 72: 42.174694 s / -5.355915 |
| `harp` | 59: 11.230635 s / -25.413563 | 72: 7.013016 s / -20.268654 |
| `pizzicato` | 60: 1.148753 s / -36.399365 | 72: 0.829365 s / -25.787316 |

All six manifest keys are available. Actual celesta and vibraphone recordings are absent from this pinned source; the explicitly named glockenspiel and tubular-bell replacements above are retained. Import completed without playback or Unity integration. The asset-phase delay came from a PowerShell quoting error before downloads, an `astats` check that initially expected float-only NaN/infinity counters from integer PCM (corrected by decoding to float for inspection), and the slower download of the complete 42-second upper tubular-bell recording. No further discovery or downloads were performed after completion.
