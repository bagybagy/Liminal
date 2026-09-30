# Liminal PV Capture

`capture-pv.ps1` launches Unity 6000.3.13f1 in the normal Editor with the `Liminal.Editor.PvCaptureSession.Run` entry point. It is a one-take workflow: the Editor opens `AbyssalChoir`, enables the `Liminal.CavernPV` preference, records one 1920x1080 Game View take at 30 fps with Unity Recorder audio, writes `LIMINAL_raw.mp4` plus `LIMINAL_raw.json`, returns an exit code, and clears the capture preferences. The launcher waits once for at most 240 seconds; the Editor-side recording limit is 79 seconds. It intentionally does not use batch mode.

The `Experience` hook reads `Liminal.CavernPV`, keeps `CavernMode=true` while setting `ProofActive=true`, and initializes `PvDirector`. After Recorder starts, the capture session resets the game and schedules the soundtrack from its actual DSP origin. During the take, the director uses `AuthoredScore.Data.beats` for shot boundaries and normal `Flight.Step`, center-reticle `Encounter.AcquireAt`, held locks, and `Encounter.Release` calls. It does not change `Time.timeScale`, grant hits, alter life, or reset the game during a shot. Game View is opened/focused through the Editor API and `Application.runInBackground` is enabled for the bounded session. A take without eight actual dolphin body locks is rejected.

The raw sidecar records `Music.DspOrigin - recordStartDsp` as `audioOffsetSeconds`, the sample rate, authored beat markers, captured frame count, and playback/combat stats. `finish-pv.ps1` reads that sidecar and `Assets/Liminal/Resources/TidalMemoryTimeline.json`; it does not infer a BPM. It keeps authored beat intervals `[0,16]`, `[48,64]`, `[64,96]`, `[96,104]`, and `[128,152]`, applying the same offset to audio and video with only 30 ms audio fades at cut edges. Batch acquisition selects actual body points inside the visible lock radius, through the same visibility, distance, reservation and hit-scheduling checks as gameplay. Locks are abandoned at shot cuts so an unfinished whale lock cannot consume a dolphin lock slot.

With `ffmpeg` and `ffprobe` on `PATH`, run:

```powershell
.\Tools\capture-pv.ps1
.\Tools\finish-pv.ps1
```

Finishing writes `Liminal_PV_High.mp4` (1920x1080, H.264 CRF 18, 16 Mb/s max, AAC 256 kb/s) and `Liminal_PV_Git.mp4` (1280x720, H.264 CRF 21, 5 Mb/s max, AAC 192 kb/s). Both are checked for 30 fps, 45-50 second duration, stereo 48 kHz AAC, and a 95 MiB size ceiling. A contact sheet samples eight frames from the High PV. When FFmpeg's `drawtext` filter and the Windows Arial font are available, a small white `LIMINAL` overlay appears over gameplay at the opening and ending; it is never a black end card.

## 完成したPV（2026-10-01）

- `Liminal_PV_High.mp4`: 1080p / 30fps / 46.57秒 / 61.13 MiB。通常視聴・投稿用の高画質版。
- `Liminal_PV_Git.mp4`: 720p / 30fps / 46.57秒 / 20.88 MiB。軽量配布用。
- どちらもH.264 / ステレオAAC / faststart。BGMと射撃・ロック・迎撃音はUnity Recorderで混合収録したものを保持。スロー化・旧映像のループ・後付けBGMは使用していない。
- 構成: クラゲへの射撃、海蛇、渦から集まるクジラと浮上・大波、クジラへの攻撃、イルカの8点フルロックと連射、敵弾の迎撃。
- `LIMINAL_raw.json`: 今回の収録証跡。40発・40命中、8点フルロック、敵弾54発・迎撃10発。約78秒の原録画は同じフォルダにローカル保存し、完成した2本だけGitに同梱。
- `dolphin-full-lock.png`: 原録画63.08秒の8点ロック画面。`contact-sheet.jpg`は完成PVの8場面。
- `validation.json`: 寸法、尺、コーデック、音量確認と元の戦闘検証への参照。

ゲームのWindowsビルドは `../../Builds/Windows/Liminal.exe`、X向け投稿案は `../../Devlog/README.md`。
