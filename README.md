# Japanese ARPAsing Phonemizer v0.2.4

OpenUtauのARPAsing音源で、日本語のかな・カタカナ・日本語ローマ字入力をARPAbetへ変換する外部phonemizerです。

## 対応

- OpenUtau v0.1.569.0対応を目的に更新
- v0.1.568系との互換性も維持する設計
- .NET 8
- ARPAsing系音源

OpenUtau 0.1.569ではphonemizerのトラック設定まわりが変更され、`toneShift`等の属性がnullableになりました。v0.2.4では、独自処理部分が旧/新の属性レイアウトへ直接依存しない互換アクセスを使用します。

## v0.2.4 の主な変更

### 1. 撥音「ん」を安定化

`ん / ン`は常にARPAbet `n`として扱います。

```text
ん      -> n
あん    -> aa n
あんな  -> aa n n aa
さんぽ  -> s aa n p ow
りんご  -> r iy n g ow
```

以前は後続子音によって`m`または`ng`へ自動同化していましたが、ARPAsing音源によって`m b`や`ng k`等のCCが存在せず不安定になるため、v0.2.4では日本語の撥音をcanonical `n`へ統一しました。

ローマ字の`n / nn / n'`も対応します。

```text
konnichiwa -> k ow n n iy ch iy w aa
kanpai      -> k aa n p aa iy
shin'ya     -> sh iy n y aa
```

### 2. 外来音・拡張モーラを追加/全検査

代表例:

```text
ちぇ -> ch eh
つぁ -> t s aa
つぃ -> t s iy
つぇ -> t s eh
つぉ -> t s ow
てぃ -> t iy
でぃ -> d iy
とぅ -> t uw
どぅ -> d uw
しぇ -> sh eh
じぇ -> zh eh
ふぁ -> f aa
くぁ -> k w aa
```

さらに`きぇ/ぎぇ/にぇ/ひぇ/びぇ/ぴぇ/みぇ/りぇ`、`つゅ`、`てゃ/てゅ/てぇ/てょ`、`でゃ/でゅ/でぇ/でょ`、`ふゃ/ふゅ/ふょ`、`くゎ/ぐゎ`等を追加しています。

`てゅ`などのyグライドは、ARPAsingでCC不足が起こりにくいよう、必要に応じて`iy y`を経由します。

```text
てゅ -> t iy y uw
でゅ -> d iy y uw
つゅ -> t s iy y uw
ふゅ -> f iy y uw
```

## 既存機能

- ひらがな / カタカナ
- 日本語ローマ字
- 四つ仮名の分離
  - `じ / ji / zi -> zh iy`
  - `ぢ / di -> jh iy`
  - `ず / zu -> z uw`
  - `づ / du -> d uw`
- `dhi -> でぃ -> d iy`
- yグライド前寄せ
- 同一歌詞内の長音記号`ー`
- 大文字`R`による語尾音
- ひらがなラ行 = `r`
- カタカナ ラ行 = `l`（意図的なL入力）
  - `ら -> r aa`
  - `ラ -> l aa`
  - `りゃ -> r iy y aa`
  - `リャ -> l iy y aa`

## テスト

v0.2.4では以下を自動検査しています。

- KanaMap 全197エントリ
- ひらがな→カタカナ正規化 179ケース
- ローマ字辞書 全200エントリ
- 外来音・拡張モーラ 明示136ケース（ひらがな/カタカナ）
- `ん`の単独・語中・連続・カタカナ・ローマ字ケース
- ARPAbet出力記号の妥当性
- OpenUtau 0.1.569型のnullable `PhonemeAttributes` APIとのコンパイル互換テスト
- 0.1.569型APIスタブ上での`ん`実行テスト（単独/語中/カタカナ）

基準音源「轟音ロキ ARPAsing Lite」のoto監査では、KanaMap 197エントリについて、内部遷移305/305、開始エイリアス197/197、語尾エイリアス197/197を確認しています。これは基準音源での監査結果であり、他のARPAsing音源で全エイリアスが存在することを保証するものではありません。

## インストール

`JapaneseArpasingPhonemizer.dll`をOpenUtauへドラッグ&ドロップして再起動し、phonemizerから

`[JA ARPA] Japanese ARPAsing Phonemizer`

を選択してください。

## 注意

- 英単語G2Pは実装していません。アルファベット入力は、ARPAbet直接入力として成立する場合を優先し、それ以外は日本語ローマ字として解析します。
- 音源によってARPAsingエイリアス構成は異なります。
- `zh`や`l`等がない音源ではfallbackや別エイリアスが必要になる場合があります。
- OpenUtau 0.1.569実機上での最終動作確認はWindows側で行ってください。
