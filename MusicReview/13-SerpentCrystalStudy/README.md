# 海蛇SE / ガラスの共鳴による試聴

前回Cは、既存のFM音の変調量と倍音を抑えただけの音色でした。
ユーザーレビューではB/Cの音階は好まれたものの、Cは澄んだガラスに聞こえませんでした。
今回はFMを使わず、ガラス楽器の実測モード比を参考にしたモーダル合成へ変更します。
計測値そのものは参考にしていますが、音量と減衰はゲーム向けに設計した値で、
特定の楽器を忠実に再現した物理シミュレーションではありません。

| ファイル | 試作の方向 |
| --- | --- |
| F_ClearGlassBell | 周波数比1:2.43:4.36。立ち上がりを短くし、高い共鳴も聞かせるベル寄り |
| G_SoftGlassBowl | 周波数比1:2.54:2.55:4.61。近接する二つの共鳴が自然に干渉する、柔らかなガラス鉢寄り |
| 各_Solo | 最初の8連射をBGMなしで聞く6秒の補助音源 |

約15秒の同じ海蛇BGMに3回の8連射を配置。Bで好まれた折り返しのフレーズを
1オクターブ上で鳴らします。1連射の途中で音域を上へ積み上げる動作はありません。
F/Gは同じ音高・時刻・BGM・ゲインです。各音の積算エネルギーを同じ音高の
現行SEへ合わせていますが、周波数分布が違うため知覚音量の一致を保証するものではありません。
前回B/Cとは音域も変えているので、音色だけを厳密に比較する実験ではありません。
ゲームには未採用。ボス撃破SE・TidalMemory・他の曲・公開Release・Windowsビルドは変更しません。

## 参考と再生成

[Thomas D. Rossing, Acoustics of Glass Musical Instruments, Glass Music World Winter 2004](https://glassmusicintl.org/winter_2004.pdf)、紙面2〜3ページのFinkenbeinerガラスボウルの実測値を参照。
[Harvardのワイングラスの固有モード解説](https://sciencedemonstrations.fas.harvard.edu/presentations/shattering-wineglass)も確認しました。
実録音や第三者のサンプル素材は使用していません。

Unityの `Liminal/Export Modal Glass Comparison`、または
`-executeMethod Liminal.Editor.SerpentAudioStudy.ExportModalComparison` を使用します。
MP3はFFmpeg / libmp3lameの320kbps。WAVはローカルに保持し、MP3と数値レポートをコミットします。
この段階では「澄んだガラスに聞こえる」という評価は未確定で、試聴で判断します。
