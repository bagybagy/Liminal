# LIMINAL解説PV 一次資料メモ

調査範囲: `C:/repos/Antigravity/Liminal` のプロジェクト資料・コードのみ。行番号はこの調査時点の参照位置。

## 正式タイトル

表記は **LIMINAL / ABYSSAL CHOIR**。READMEとDESIGNの見出し、および既存PVのエンドタイトルで一致する。「アビサルキュリオ」ではなく `ABYSSAL CHOIR`。

## 作品の核

LIMINALは、発光する五つの海底洞窟を自由に泳ぎ、音楽に結びついたロックオン射撃で生物と戦うオリジナル音楽シューティング。撃破で粒子が消えるのではなく、植物・群落・遺跡などへ同じ粒子個体が移り、攻略の痕跡が世界に残る。クジラからイルカ、さらにアトランティスへ粒子を受け継ぐ流れが、映像で伝えやすい中心モチーフ。

## 技術・制作上の確かな説明

- **GPU粒子**: 専用Compute Shaderが位置・速度・寿命を更新し、GraphicsBufferの粒子をURPのGraphics.RenderPrimitivesで描画。262,144粒の海蛇、長期状態を保つ海洋粒子群などを扱う。Unity VFX Graphを使ったという説明は誤り。
- **粒子の見た目**: 描画方式はQuad。Original Quadが現在の標準で、SharpQuadは比較・切り替え可能な選択肢。SharpQuadではテクスチャなしの解析的な光芯、粒径上限、普通の光と短く輝く粒の分離、独立した揺らぎを使う。全体Bloomを増やして粒子数だけで見せているわけではない。
- **音楽同期**: AudioSource.PlayScheduledとDSP時計を音楽・射撃・譜面・粒子変形の共通基準にする。採用曲の実音源から拍位置を推定してタイムライン化するが、拍・和声は自動解析の推定で、人手で確定した採譜ではない。
- **VR**: Windows版でデスクトップとPCVRを切り替え、OpenXR MultiPass、ワールド空間HUD、スティック操作を用意。Quest Link/Air Link等でPCへ接続する想定。Quest単体版ではなく、実HMDの操作感・快適性・負荷を検証済みとは言えない。
- **生成音楽**: 2026-10-05採用の4つの独立した歌はACE-Step XL-SFTと1.7B言語モデルのtext2musicで生成。歌詞・旋律は独立生成で、既存音源や歌手の声を入力していない。曲ごとの使用承認記録がある。プロジェクト内の全音・全素材がAI生成だとは説明しない。
- **制作体制**: 記録から担当割り当てを確認できたワーカー75名が名簿に載る。内訳は完了報告69、停止2、未報告4。名簿は参加記録であり、全員の成果物が製品へ統合されたという意味ではない。制作者`tete`は別枠。技術クレジットにはUnity 6、URP、Compute Shaders、GraphicsBuffer、DSP予約音声、OpenXR、ACE-Step XL-SFT、Unity Recorder / FFmpegが記録されている。

## 60〜90秒版の字幕案（合計約72秒）

| 目安 | 短い字幕 | 根拠・映像上の注意 |
|---|---|---|
| 0–7秒 | **LIMINAL / ABYSSAL CHOIR**<br>音と光が息づく、五つの海底洞窟へ。 | `README.md:1-4`。タイトルはこの英字綴りで固定。 |
| 7–16秒 | **狙いをなぞり、八つまでロック。**<br>放つ弾も、音楽の拍へ。 | `README.md:54-63, 115-121`。今回添付の10/4録画から該当場面を選ぶ。 |
| 16–26秒 | **一粒ずつが、形をつくる。**<br>GPU上で動き続ける、海の光。 | `README.md:126-130`、`Assets/Liminal/Runtime/PersistentMatter.cs:51-57, 81-88, 255-266, 288-301`。Compute Shader / GraphicsBuffer / URP描画の説明。 |
| 26–36秒 | **倒しても、消えない。**<br>生き物の粒子が、海底の命へ。 | `SHARP_MATTER.md:26-38`、`README.md:91-98`。植物化・群落化の実プレイまたは検証済み最新画面。 |
| 36–47秒 | **大渦から現れる、ホエール。**<br>光の身体はイルカへ受け継がれる。 | `README.md:75-77`。今回添付の10/4録画から該当場面を選ぶ。 |
| 47–57秒 | **PCVRにも対応。**<br>OpenXRで、洞窟を自由に遊泳。 | `README.md:176-188`、`Assets/Liminal/Editor/PcVrSetup.cs:80-115`。映像はPCVR実動作の収録に限定し、未検証のHMD互換性をうたわない。 |
| 57–66秒 | **歌も、世界のために生成する。**<br>拍を読み取り、光とゲームへ結ぶ。 | `MusicReview/12-VocalReferences/README.md:3-12`、`STAGE_MUSIC_ADOPTION.md:19-28`。AI生成曲の使用は採用・承認記録がある曲に限る。 |
| 66–72秒 | **75名の担当記録と、制作技術。**<br>粒子の海は、まだ続いていく。 | `CONTRIBUTORS.md:3-9`。75名は割当記録上の参加者数。全員が出荷済み成果を作ったとは言わない。エンドロールにモデル名と技術名を載せる場合も名簿の記録に揃える。 |

字幕はPVの短い既存コピーの調子に合わせ、照準・ロック表示を避けた画面上配置を想定。正式タイトルのほか、説明文を一画面に詰め込まない。

## 既存PVを再利用するときの境界

リポジトリの既存PVは約38.7秒で、原録画は**2026-10-01**。クラゲ、海蛇、クジラ登場・着水、クジラの粒子剥離、イルカ8点ロックが含まれる。ヤドカリ、潜水艦、アトランティス、VR操作は含まれない。今回の素材はユーザー添付の**2026-10-04録画**であり、10/1の既存PVとは別に扱い、字幕に使う場面は添付録画内で確認できるものから選ぶ。2026-10-05公開の条件付きエンディング、180秒スタッフロール、全撃破時の追加生物を10/1または10/4の録画に映っていると断定しない。

既存PVの編集はゲームプレイを主役にし、黒い説明カードを避け、短文を照準から外している。原録画のTidalMemoryとゲームSEを映像と同区間で使い、スロー・ループ・挙動捏造・新規BGM差し替えをしていない。確認用の原録画統計は編集後のPV統計ではない。

## 音源の利用根拠と注意

- 既存PVの確認済み音はUnity Recorder収録のTidalMemoryとゲームSE。既存PVの音楽・編集来歴を維持するなら、元ゲーム音を同じ映像区間から使う。
- 2026-10-05のエンディング4曲はACE-Stepの独立生成曲。`Tools/approved-ending-audio.json`は「direct user approval」と「Original ACE-Step generation; user confirmed adoption is permitted」を採用根拠として記録する。エンディング曲を使う場合はこの承認済み素材から選ぶ。
- ステージ3曲もACE-Step生成で参照音源・Coverを使わない採用記録がある。`STAGE_MUSIC_ADOPTION.md:19-20`。ハープSEはCC0のVSCO-2-CE録音由来と記録されている（同:68-78）。
- 原作の音源・モデル・テクスチャ・商標ロゴは本作に含めない（`README.md:3-5`）。市販・第三者の生素材をPVへ追加する根拠は見つからないため、追加素材は避ける。

## 参照

- タイトル・作品・システム: `README.md:1-5, 54-63, 71-100, 113-130, 176-188`
- GPU粒子の表現: `PARTICLE_DESIGN.md:11-18`, `SHARP_MATTER.md:5-13, 26-42`
- PCVR/OpenXR: `Assets/Liminal/Editor/PcVrSetup.cs:80-120`; `README.md:176-188`
- 生成曲と利用根拠: `MusicReview/12-VocalReferences/README.md:3-12`; `ENDING_RELEASE.md:15-27, 33-43`; `Tools/approved-ending-audio.json:1-5`; `STAGE_MUSIC_ADOPTION.md:19-28, 68-78`
- 参加記録: `CONTRIBUTORS.md:3-9`
- 既存PVの構成・音・日付: `Video/PV/README.md:1-28`; `Video/PV/final-edit.json:1-8, 66-85`; `Video/PV/validation.json:1-12`
