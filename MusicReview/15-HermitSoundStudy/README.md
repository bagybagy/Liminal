# hermit offline SE audition

Pre-adoption comparison only; candidates are not integrated into the game. No playback was performed.
This is a middle high-energy candidate selected by sustained RMS, not a human-confirmed chorus. Beat and harmony estimates have not been verified by human listening.

Game-adopted WAV: [Hermit_OrchestralCurrent](../../Assets/Liminal/Resources/StageAudio/Hermit_OrchestralCurrent.wav), 84.979229--96.595011 seconds; zero-based beat 176 through 200.
Parent selection: middle third, 16-beat boundaries, 24 beats, 75% window RMS + 25% minimum bar RMS. Intro RMS 0.071508; window RMS 0.145525. Original raw offset adds 0.203175 seconds.

Each clip is 13.715782 seconds: 2.1 seconds of solo (onset 0.12), then the same 24-beat BGM; two eight-shot phrases at beat offsets 4 and 16, using saved eighth marks. SE tails fit completely inside the window.
A_Current retains the game FM PCM amplitude, ascending AuthoredScore.Note pitches, pan 0 and strength 1; it is not energy-normalized or boosted in the high register.
New candidates reuse the unchanged tone() and B folding [0,2,1,0,1,2,1,0], MIDI 60..78. Tubular Bells is the actual recorded instrument behind the legacy sample key vibraphone.

Fixed mix: BGM .83, SE .62, master .8. Shared additional gain 1, adjusted 0 time(s). No per-candidate mix normalization or limiter. Common 15 ms outer edge fades; no BGM time stretching.
Local stereo PCM16 WAVs are retained; MP3s are 320 kbit/s. Recorded samples use the existing CC0-1.0 manifest and [source documentation](../../Tools/se-audition-samples/SOURCES.md).

[Candidate index](INDEX.md) and [report](study-report.json) contain source/generator/output hashes, original times, MIDI/onsets, levels and numeric verification.
