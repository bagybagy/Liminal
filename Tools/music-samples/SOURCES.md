# VSCO-2-CE sample subset

Source: [VSCO-2-CE SFZ branch at commit `6dd651d55dde97fd4028699be9d4481f26917891`](https://github.com/sgossner/VSCO-2-CE/tree/6dd651d55dde97fd4028699be9d4481f26917891), maintained by Versilian Studios LLC.

Attribution: VSCO 2 CE recordings by Sam Gossner and Simon Dalzell; sample cutting by Elan Hickler / Soundemote. The official [Versilian Studios license page](https://versilian-studios.com/vsco-community/) identifies the library as Creative Commons Zero / public domain; the repository includes its [CC0-1.0 license](https://github.com/sgossner/VSCO-2-CE/blob/6dd651d55dde97fd4028699be9d4481f26917891/LICENSE).

The sample mappings come from the official SFZ files below. MIDI values in `samples.json` are each region's `pitch_keycenter`, not inferred from filenames. The WAVs were fetched individually from the pinned GitHub revision (no archive or full-library download) and converted with installed FFmpeg to mono, 44.1 kHz, signed PCM16. The original durations were retained.

| Instrument | MIDI root | Output | SFZ sample and exact source URL |
|---|---:|---|---|
| French horn | 57 | `horn-57.wav` | [MOHorn_sus_A2_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Brass/F%20Horn/sus/MOHorn_sus_A2_v1_1.wav) |
| French horn | 60 | `horn-60.wav` | [MOHorn_sus_C3_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Brass/F%20Horn/sus/MOHorn_sus_C3_v1_1.wav) |
| French horn | 74 | `horn-74.wav` | [MOHorn_sus_D4_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Brass/F%20Horn/sus/MOHorn_sus_D4_v1_1.wav) |
| Cello section | 40 | `cello-40.wav` | [susvib_E1_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Cello%20Section/susvib/susvib_E1_v1_1.wav) |
| Cello section | 47 | `cello-47.wav` | [susvib_B1_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Cello%20Section/susvib/susvib_B1_v1_1.wav) |
| Cello section | 53 | `cello-53.wav` | [susvib_F2_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Cello%20Section/susvib/susvib_F2_v1_1.wav) |
| Cello section | 60 | `cello-60.wav` | [susvib_C3_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Strings/Cello%20Section/susvib/susvib_C3_v1_1.wav) |
| Flute | 64 | `flute-64.wav` | [LDFlute_susvib_E3_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Woodwinds/Flute/susvib/LDFlute_susvib_E3_v1_1.wav) |
| Flute | 69 | `flute-69.wav` | [LDFlute_susvib_A3_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Woodwinds/Flute/susvib/LDFlute_susvib_A3_v1_1.wav) |
| Flute | 72 | `flute-72.wav` | [LDFlute_susvib_C4_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Woodwinds/Flute/susvib/LDFlute_susvib_C4_v1_1.wav) |
| Flute | 76 | `flute-76.wav` | [LDFlute_susvib_E4_v1_1.wav](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/Woodwinds/Flute/susvib/LDFlute_susvib_E4_v1_1.wav) |

SFZ mappings: [FHornSus.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/FHornSus.sfz), [CelloEnsSusVib.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/CelloEnsSusVib.sfz), and [FluteSusVib.sfz](https://raw.githubusercontent.com/sgossner/VSCO-2-CE/6dd651d55dde97fd4028699be9d4481f26917891/FluteSusVib.sfz).

None of these regions defines SFZ loop points, and the source WAVs have no embedded `smpl` loop chunk, so `loopStart` and `loopEnd` are `null`. Full source recordings (about 7.6-15.2 seconds) are retained; a roughly 3-second playback hold may truncate their natural tails because no sustain loops are provided.
