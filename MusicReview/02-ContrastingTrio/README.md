# LIMINAL 音楽リテイク 02：異なる聞き味の3曲

試聴用の候補曲です。ゲームのBGMはまだ差し替えていません。
Tidal Bloomの旋律、コード進行、音源コードを流用せず、3曲を別々に作曲・編曲しています。

## 試聴

| 曲 | 長さ | 狙いと主役 | 聞きどころ |
| --- | --- | --- | --- |
| Lantern Current | 2:35 | 温かいブロークンビート。エレクトリック・ピアノ風鍵盤、リード系の中音域旋律、丸いベース | 冒頭から鍵盤とベース。0:15から主題、0:46から異なる2音色の受け答え、1:17から和声を変える橋渡し、1:33から厚い再現 |
| Scarlet Vector | 3:06 | 機械的なメロディック・エレクトロ。太いアナログ・ブラス、矩形波ベース、低いタム | 冒頭から駆動リフ。0:15から主題、0:46から噛み合う機械の音型、1:17で拍の密度を落とし、2:04から最大展開 |
| Horizon Canticle | 2:50 | 雄大なハーフタイムの歌。実録チェロ／ホルン／フルート、揺らぐ合成ストリングス | 冒頭はチェロ独奏。0:07からホルン、0:46からフルートとチェロ、1:17から明るい和声、1:48から厚い主題再現 |

すべて124 BPM・4/4。整数小節の構成とDを中心にした調性は接続を想定した共通条件です。
ただし本ファイルは終止と余韻を含む試聴版で、無加工のシームレスループとしては扱いません。
使用音色、ドラム配置、和声の動き、旋律の音価、冒頭、クライマックスの位置を曲ごとに分けています。
高音の短いベル連打を主旋律にする設計は採用していません。

## 調査した方法と適用

- Ableton / Dennis DeSantis [Creating Melodies 1: Contour](https://makingmusic.ableton.com/creating-melodies-1-contour)：上行と下行、跳躍と順次進行の釣り合い、句の頂点を考えて、記譜された主題と応答を作成。コードの構成音をランダムに鳴らす方法から変更。
- Ableton [Arranging as a Subtractive Process](https://makingmusic.ableton.com/arranging-as-a-subtractive-process)：全パートを常時重ねず、主題・応答・間奏・再現で抜くパートを決める。低密度区間も無意味な空白にはせず、旋律や和声の役割を残す。
- Ableton [Dramatic Arc](https://makingmusic.ableton.com/dramatic-arc)：素材提示、発展、頂点、解放を設計。3曲の頂点の時刻と、その前に緊張を作る方法を変える。
- Ableton [Linear Rhythm in Melodies](https://makingmusic.ableton.com/linear-rhythm-in-melodies)：Lanternの応答とScarletの中間部で、音色同士が交互に旋律を担当するホケットを使用。
- Attack Magazine [Breakbeat Techno](https://www.attackmagazine.com/technique/beat-dissected/breakbeat-techno/)／[Rolling Techno](https://www.attackmagazine.com/technique/video-tutorials/beat-dissected-rolling-techno/)：キックと他の打音の重なりを整理し、弱い補助打音、タム、ずれた細分拍で推進力を作る。元記事の曲やサンプルをコピーせず、独自のドラムパターンと合成打楽器を使用。
- Gordon Reid / Sound On Sound [Synthesizing Brass Instruments](https://www.soundonsound.com/techniques/synthesizing-brass-instruments)：音量と明るさの時間変化を分離し、発音時の立ち上がり、持続中の変化、遅れて入る微弱なビブラートをブラスに実装。
- Gordon Reid [Synthesizing Strings: String Machines](https://www.soundonsound.com/techniques/synthesizing-strings-string-machines)：単なるサイン波パッドではなく、僅かにずらした複数の鋸歯状波と異なる周期の変調で合成弦の厚みを作る。

これらは制作判断の根拠であり、専門家による品質認定ではありません。音楽としての合否は試聴で確認します。

## 制作手順・音源・検証

作曲・編曲と音源コードの制作はCodexの本セッション。Node.jsで手続き合成・録音サンプル再生を行い、FFmpegでミックス後の2パス・ラウドネス処理とMP3化を実施。
外部の音楽生成モデルは使用していません。実録楽器の準備のみGPT-6 Lunaワーカーに委任しました。

Horizonの録音素材はVersilian StudiosのVSCO 2 Community Edition（CC0）。元曲の録音ではなく、独立した楽器の単音素材です。
使用ファイルとソースの詳細は `../../Tools/music-samples/SOURCES.md` に保存しています。

ラウドネス目標はTidalMemoryの実測約-16.19 LUFSに合わせて-16.2 LUFS。
WAVとMP3のトゥルーピーク、クリップ、サンプル数、ステレオ位相の指標を保存。
音量を揃えたこと自体を、旋律や聞き味の良さの証明には使いません。

各曲の `.wav` はPCM16・44.1 kHzステレオ、`.mp3` は256 kbps。
`.timeline.json` に実音源のハッシュ・拍位置・構成・和声・各発音イベント、`.metrics.json` に測定値と制作方法を保存。

再現コマンド：`node Liminal/Tools/compose-review-trio.mjs`
1曲だけなら末尾に `LanternCurrent`、`ScarletVector`、`HorizonCanticle` を指定可能。
Node.jsとFFmpegが必要です。再生成は試聴フォルダだけに出力し、ゲーム資産は変更しません。
