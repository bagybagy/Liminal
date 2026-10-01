# Tidal Bloom / Audition 01

Listening candidate for the LIMINAL soundtrack retake. Not selected for the game.
TidalMemory and all installed game audio remain unchanged.

- Full mix: `TidalBloom.wav` (44.1 kHz stereo, 16-bit PCM).
- Listening copy: `TidalBloom.mp3` (256 kbps).
- Length: 3:05.81; 124 BPM, 4/4, 96 bars.
- Method: authored procedural synthesis in `../../Tools/compose-retake.mjs`.
  No external music generation model, sample pack, or third-party recording.
- Reference: the original TidalMemory drum and synthesis approach, with a new
  eight-bar melody, offbeat bass, selective delay sends, and an authored build.
- Listen for the melody from 0:15, the break from 1:17, and the return from 1:48.

The authoring assistant created the arrangement and synthesis code; Node.js rendered
the audio. A code-generation model is not an audio-generation model.

## Measurements

FFmpeg loudnorm analysis of the source files (no normalization filter was applied
to either delivered file):

| Source | Integrated LUFS | True Peak dBTP | Loudness Range LU |
| --- | ---: | ---: | ---: |
| TidalMemory | -16.19 | -3.58 | 9.30 |
| Tidal Bloom | -16.46 | -3.43 | 9.30 |

The source mix matches TidalMemory's RMS and has zero clipped PCM samples.
`render.json` records the source hashes, arrangement sections, and render metrics.
`TidalBloomTimeline.json` records notes, harmony, beats, and sections in samples.
These checks establish a usable listening file, not musical acceptance.

## Reproduction

From the repository root:

```powershell
node Liminal/Tools/compose-retake.mjs
ffmpeg -y -i Liminal/MusicReview/01-TidalBloom/TidalBloom.wav -codec:a libmp3lame -b:a 256k Liminal/MusicReview/01-TidalBloom/TidalBloom.mp3
```

Listener approval of rhythm, melody, timbre, and progression is required before
making this a stage theme or authoring the other themes around it.
