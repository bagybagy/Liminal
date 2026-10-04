# ステージ音楽の採用 / 2026-10-04

指定された4つのWAVを採用。クラゲ部屋とクジラ戦のTidalMemoryはファイルも音色も変更しません。
公開Releaseはv0.1.10のまま、今回の内容は開発ビルドです。

| 場所 | 採用名 | 指定元ファイル |
| --- | --- | --- |
| クラゲ / 最初の部屋 | TidalMemory | 従来の原曲 |
| 海蛇 | Serpent_VelvetKeys | c2d26991-51c7-2cd2-6776-d0dfb7de3f7a.wav |
| クジラ | TidalMemory | 従来の原曲 |
| ヤドカリ | Hermit_OrchestralCurrent | 9e27d2c9-840d-8b8c-af34-e5c7e5677668.wav |
| 潜水艦 | Submarine_OrganicCurrent | c37979f0-1392-653a-2fd4-7cd5249083e7.wav |
| エンディング / アトランティス | Ending_BreathingLine | 6c0a4538-28c2-a63a-09df-e38896cfdf1d.wav |

採用版は `Assets/Liminal/Resources/StageAudio/` に格納。
元音源はMusicReviewの指定場所にそのまま残しています。不採用だった旧StageAudioは
`MusicReview/ArchiveRejectedStageAudio/` に保管し、Resourcesから除きました。
生成元・SHA-256・処理値は `Assets/Liminal/Resources/StageAudio/ApprovedTracks.json` に記録。
4曲はACE-Step `acestep-v15-xl-turbo` / `acestep-5Hz-lm-1.7B` のtext2musicによるもので、
生成記録の参照音源・カバー元音源は未使用です。

## 処理と同期

124 BPMの生成指定から機械的に拍を作らず、実音源の拍をlibrosaで推定し、
44.1kHzのサンプル位置として保存します。拍の中点を8分音符に使い、
射撃・命中・粒子の脈動・曲切り替えは同じタイムラインを参照します。
小節頭と和声は自動解析の推定であり、確定譜面や人手で検証した採譜ではありません。
和声はHPSSで分離した調性成分のchromaから三和音候補を選び、射撃音と撃破音に使います。

元録音は48kHz float。採用版は44.1kHz stereo PCM16で、小節単位の切り出しと
ループ端5msのクリック防止を適用。テンポ伸縮・移調・アレンジ変更はしていません。
音量はTidalMemoryの測定値約-16.19 LUFSへ寄せる単純ゲイン調整です。
圧縮・リミッターは加えず、True Peakの余裕を優先するため潜水艦曲は目標よりわずかに低めです。
曲切り替えは出側の4小節境界で開始し、入側の2拍でクロスフェード。
異なる調性が長く重なるのを避け、切り替え後も累積拍位置を引き継ぎます。

| 採用名 | 元音源からの区間（秒） | 小節 | 拍間隔中央値からの参考BPM | ゲイン |
| --- | --- | --- | --- | --- |
| Serpent_VelvetKeys | 1.231〜208.550 | 108 | 124.53 | -2.20 dB |
| Ending_BreathingLine | 0.760〜208.567 | 107 | 123.05 | -0.88 dB |
| Submarine_OrganicCurrent | 0.058〜208.347 | 108 | 124.53 | -0.50 dB |
| Hermit_OrchestralCurrent | 0.203〜208.585 | 108 | 124.53 | -2.44 dB |

参考BPMの数値はゲーム時計に使いません。解析の分解能は約5.8msで、
小節頭・複雑な打楽器・ボーカル区間では推定誤差があり得ます。最終的な聴感レビューは必要です。

## ボス撃破の3音

生成フォールバックは現在の和声から3音を選び、連続する8分音符でクリスタル音を鳴らします。
撃破イベントは1回、発音は3回。`BossAudio.asset` に差し替え音源を指定した場合は、
その完成フレーズを1回再生し、3回重複させません。

## 再生成と検査

- `python Tools/adopt-stage-audio.py`: 指定元音源を解析。
- `python Tools/adopt-stage-audio.py --prepare`: 保存した解析からWAVとタイムラインを作成。
- `node Tools/audit-stage-music.mjs`: ファイル・ハッシュ・拍・音量・インポート設定を検査。
- Windowsビルドの `--verify-stage-audio --background-proof --output <absolute-path>`: 無音で割り当て、実DSP切り替え、拍位相、3音の予約と差し替え、ポーズ・再開始を検査。

プレイ中に音楽解析や生成サービスを走らせることはありません。

Windowsビルドの限定検査は合格。海蛇→ヤドカリ→潜水艦→クジラ→エンディングの
実DSP切り替え、割り当てとサンプル数、拍位相の連続性、3音の予約、差し替え音源の1回再生、
ポーズ中の時計保持とTidalMemoryへの再開始を確認しました。
結果は `Builds/Windows/StageAudioReport.json`。再生位置照会の最大誤差は約21ms、
撃破音の予約時刻は保存された8分音符グリッドに一致しています。
今回のゲーム内聴感・解析された小節頭と和声の音楽的な正しさ・実HMDは未確認です。

参考: [librosaの拍追跡](https://librosa.org/doc/0.11.0/generated/librosa.beat.beat_track.html)、
[chroma](https://librosa.org/doc/0.11.0/generated/librosa.feature.chroma_stft.html)、
[UnityのDSP予約再生](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayScheduled.html)。
