# LIMINAL / ABYSSAL CHOIR — 解説PV

約90秒の日本語字幕付きPV。ユーザー提供の通常版・PCVR版の録画を切り出し、Remotion / React / SVGで制作した動く概念図を重ねます。正式名称は **LIMINAL / ABYSSAL CHOIR（アビサル・クワイア）**、制作者は **tete**。

完成ファイル: `out/LIMINAL_ABYSSAL_CHOIR_ExplainerPV_1080p.mp4`。

## 内容

| 秒 | 内容 |
| --- | --- |
| 0–6 | ホエールと作品名 |
| 6–16 | 自由遊泳とロックオン射撃 |
| 16–30 | 粒子の個体IDを保つ形態継承の概念図 |
| 30–42 | 海蛇の共有骨格、GPU粒子更新、URP描画 |
| 42–54 | 共通DSP時計による音と射撃の同期 |
| 54–64 | 添付PCVRミラー映像 |
| 64–76 | 人の判断、AIによる実装、実画面検証と修正 |
| 76–90 | ホエールと粒子都市、作品名・制作者 |

ゲーム内の実装技術はUnity 6、Compute Shader、GraphicsBuffer、URP、DSP予約音声、OpenXR。採用曲と音楽の制作記録にはACE-Stepも含まれます。今回のPVそのものはRemotionでレイアウト・フレームアニメーションを作り、FFmpegで素材の切り出し・原音ミックス・ラウドネス処理・H.264書き出しを行います。ゲームがRemotionやThree.jsで実装されているという説明はしません。

描画の概念図は実ゲームの粒子データを可視化したものではありません。音同期図の動きは説明用で、実録画からの拍の実測ではありません。画面内にも「概念図」と表示します。図と実映像の説明文の根拠は `facts.md` に記録。

## 素材・音

- 通常版: `LIMINAL 2026-10-04 23-59-43.mp4`
- PCVR版: `LIMINAL 2026-10-04 14-16-59.mp4`
- PCVR版は左右眼SBSではなく単一のミラー映像。左右眼の分離や擬似的な立体化は行いません。
- 原本は変更せず、各カットの映像と音を同じ原録画区間から抽出。速度変更や別区間からの効果音追加は行いません。
- 冒頭メニュー・長い無音を除外し、左右と上下を小さくクロップ。音は短い切り口のフェードと、全体で-16 LUFS / true peak -1.5 dBを目標に二段階正規化。
- 追加ナレーションなし。日本語字幕で無音視聴にも対応します。
- 10月5日追加の条件付きEDや追加生物を、10月4日の録画に存在すると説明しません。

## 再編集

Node.js、Python、FFmpeg、FFprobeがPATHに必要。元録画の場所は `edit-plan.json` で変更します。Windows標準フォントはローカル利用のみで、フォントファイルを配布しません。

```powershell
cd C:/repos/Antigravity/Liminal/Video/ExplainerPV
npm.cmd ci
python prepare-media.py edit-plan.json
npm.cmd run studio
npm.cmd run render
ffmpeg -y -i work/master.mp4 -vf "scale=in_range=pc:out_range=tv:in_color_matrix=bt601:out_color_matrix=bt709" -c:v libx264 -preset medium -crf 20 -maxrate 8M -bufsize 16M -threads 4 -pix_fmt yuv420p -color_range tv -colorspace bt709 -color_trc bt709 -color_primaries bt709 -c:a copy -movflags +faststart out/LIMINAL_ABYSSAL_CHOIR_ExplainerPV_1080p.mp4
python verify-video.py
```

- `public/video-manifest.json`: 尺、字幕、図の種類、切り出した映像への参照
- `edit-plan.json`: 元録画、開始秒、尺、クロップ、選定理由
- `prepare-media.py`: 同一区間からの映像・音声抽出と音量処理
- `src/video.tsx`: Remotionの構成とフレームアニメーション
- `storyboard.md`: 初期構成の判断記録
- `facts.md` / `facts.json`: 一次資料と説明の根拠
- `analysis/report.md` / `analysis/shotlist.json`: 素材分析と候補区間

元録画、切り出し素材、Windowsフォント、依存関係、一時書き出しはGitに含めません。公開先への投稿はこの制作作業に含めていません。

## 確認

全9場面の中央フレームで、録画の表示・文字のはみ出し・図の説明を確認。完成MP4は `verify-video.py` で全編デコード、90秒の尺、1080p / 30fps / H.264 / AACステレオ、95MiB以内、実音量を確認します。実測値は `validation.json` に保存。

完成版の実測: **90.005秒 / 77.41MiB / -16.1 LUFS / true peak -1.45 dBTP**。全編デコード成功、BT.709のリミテッドレンジを確認済み。

Remotion中間出力のフルレンジBT.601を、配布版で通常のリミテッドレンジBT.709へ変換します。色レンジのタグだけを付け替えず、画素値も変換して明暗を保ちます。
