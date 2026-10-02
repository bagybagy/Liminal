# LIMINAL / 39秒PV

## 現行版 / 2026-10-02

X向けに、約38.7秒・20小節のゲームプレイPVへ編集しました。
2026-10-01のUnity Recorder原録画を再利用し、原録画のBGM（TidalMemory）と
射撃・ロック・命中音を映像と同じ区間で編集しています。新曲への差し替え、
スロー化、映像の繰り返し、ゲーム挙動の捏造は行っていません。
ヤドカリ・潜水艦・アトランティス・VR操作の紹介映像は今回には含みません。

| 秒 | 見せ場 |
| --- | --- |
| 0-3.9 | 発光するクラゲと最初の射撃。「音を、泳ぐ。」 |
| 3.9-11.6 | 海蛇の回遊と射撃。「一撃が、光になる。」 |
| 11.6-23.2 | 青い渦からクジラが形成され、跳躍・着水する見せ場 |
| 23.2-31.0 | クジラの体光変化と粒子の剥離。「その光は、世界に還る。」 |
| 31.0-38.7 | イルカの8点ロック・連射。最後はLIMINAL / ABYSSAL CHOIR / tete |

黒い説明カードを挟まず、ゲーム画面上に短い文字を配置します。
日本語は小さなタイプライター式の出現、タイトルは控えめな移動とフェード。
中央の照準とロック表示を避け、長い機能説明や未公開URLは入れません。
発光色を保った軽いSDR補正と、測定値を使った二段階ラウドネス正規化
（-16 LUFS / true peak -1.5 dB目標）を適用します。

- `Liminal_PV_High.mp4`: 1920x1080 / 30fps / H.264 / AAC 256kbps。通常視聴・投稿用。
- `Liminal_PV_Git.mp4`: 1280x720 / 30fps / H.264 / AAC 192kbps。軽量配布用。
- `final-edit.json`: 元録画の区間、字幕、音量測定値、出力仕様。戦闘統計は原録画全体の値と明記。
- `final-titles.ass`: UTF-8字幕とタイプライター表示の編集原稿。フォントファイルは配布しない。
- `final-title.png` / `contact-sheet.jpg`: 完成映像から取得したタイトルと8場面。

再編集はNode.js、FFmpeg、FFprobeがPATH上にあるWindows環境で実行します。
Windows標準のYu Gothic、Bahnschrift、Consolasを使用します。

```powershell
node .\Tools\finish-pv-final.mjs
```

原録画と一時作業ファイルはローカル保存、完成映像と編集原稿をGitに含めます。
両出力の尺・寸法・コーデック・音声・95MiB上限を検査し、全編のデコードを確認します。
編集方針はゲームプレイを主役にするSteamworksの案内とも一致させています。
https://partner.steamgames.com/doc/store/trailer

## 原録画の収録手段

`capture-pv.ps1` launches Unity 6000.3.13f1 in the normal Editor with the `Liminal.Editor.PvCaptureSession.Run` entry point. It is a one-take workflow: the Editor opens `AbyssalChoir`, enables the `Liminal.CavernPV` preference, records one 1920x1080 Game View take at 30 fps with Unity Recorder audio, writes `LIMINAL_raw.mp4` plus `LIMINAL_raw.json`, returns an exit code, and clears the capture preferences. The launcher waits once for at most 240 seconds; the Editor-side recording limit is 79 seconds. It intentionally does not use batch mode.

The `Experience` hook reads `Liminal.CavernPV`, keeps `CavernMode=true` while setting `ProofActive=true`, and initializes `PvDirector`. After Recorder starts, the capture session resets the game and schedules the soundtrack from its actual DSP origin. During the take, the director uses `AuthoredScore.Data.beats` for shot boundaries and normal `Flight.Step`, center-reticle `Encounter.AcquireAt`, held locks, and `Encounter.Release` calls. It does not change `Time.timeScale`, grant hits, alter life, or reset the game during a shot. Game View is opened/focused through the Editor API and `Application.runInBackground` is enabled for the bounded session. A take without eight actual dolphin body locks is rejected.

The raw sidecar records `Music.DspOrigin - recordStartDsp` as `audioOffsetSeconds`, the sample rate, authored beat markers, captured frame count, and playback/combat stats. `finish-pv.ps1` reads that sidecar and `Assets/Liminal/Resources/TidalMemoryTimeline.json`; it does not infer a BPM. It keeps authored beat intervals `[0,16]`, `[48,64]`, `[64,96]`, `[96,104]`, and `[128,152]`, applying the same offset to audio and video with only 30 ms audio fades at cut edges. Batch acquisition selects actual body points inside the visible lock radius, through the same visibility, distance, reservation and hit-scheduling checks as gameplay. Locks are abandoned at shot cuts so an unfinished whale lock cannot consume a dolphin lock slot.

The previous 46-second edit is retained as a historical script. For a fresh raw take,
run the first command; use `finish-pv-final.mjs` above for the current deliverable.

```powershell
.\Tools\capture-pv.ps1
.\Tools\finish-pv.ps1
```

Finishing writes `Liminal_PV_High.mp4` (1920x1080, H.264 CRF 18, 16 Mb/s max, AAC 256 kb/s) and `Liminal_PV_Git.mp4` (1280x720, H.264 CRF 21, 5 Mb/s max, AAC 192 kb/s). Both are checked for 30 fps, 45-50 second duration, stereo 48 kHz AAC, and a 95 MiB size ceiling. A contact sheet samples eight frames from the High PV. When FFmpeg's `drawtext` filter and the Windows Arial font are available, a small white `LIMINAL` overlay appears over gameplay at the opening and ending; it is never a black end card.

## 前版の記録（2026-10-01 / Git履歴）

以下の仕様は前版のものです。同名の完成MP4は現在、上記38.7秒版へ更新しています。

- `Liminal_PV_High.mp4`: 1080p / 30fps / 46.57秒 / 61.13 MiB。通常視聴・投稿用の高画質版。
- `Liminal_PV_Git.mp4`: 720p / 30fps / 46.57秒 / 20.88 MiB。軽量配布用。
- どちらもH.264 / ステレオAAC / faststart。BGMと射撃・ロック・迎撃音はUnity Recorderで混合収録したものを保持。スロー化・旧映像のループ・後付けBGMは使用していない。
- 構成: クラゲへの射撃、海蛇、渦から集まるクジラと浮上・大波、クジラへの攻撃、イルカの8点フルロックと連射、敵弾の迎撃。
- `LIMINAL_raw.json`: 今回の収録証跡。40発・40命中、8点フルロック、敵弾54発・迎撃10発。約78秒の原録画は同じフォルダにローカル保存し、完成した2本だけGitに同梱。
- `dolphin-full-lock.png`: 原録画63.08秒の8点ロック画面。`contact-sheet.jpg`は完成PVの8場面。
- `validation.json`: 寸法、尺、コーデック、音量確認と元の戦闘検証への参照。

ゲームのWindowsビルドは `../../Builds/Windows/Liminal.exe`、X向け投稿案は `../../Devlog/README.md`。
