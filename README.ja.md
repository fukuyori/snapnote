# SnapNote Studio

<p align="center">
  <img src="Resources/icon.svg" width="128" height="128" alt="SnapNote Studio アイコン">
</p>

**SnapNote Studio** は、Windows用の高機能なスクリーンキャプチャ＆注釈ツールです。画面の任意の領域をキャプチャし、矢印、図形、テキスト、ぼかし効果などで注釈を付けることができます。

[English README](README.md)

## 機能

### キャプチャ
- **範囲選択**: クリック＆ドラッグで画面の任意の領域を選択
- **マルチモニター対応**: 複数のディスプレイでシームレスに動作
- **高DPI対応**: 高解像度ディスプレイでも鮮明にキャプチャ

### 描画ツール
| ツール | ショートカット | 説明 |
|--------|---------------|------|
| 選択 | V | 注釈を選択・移動 |
| 矢印 | A | 矢印を描画 |
| 線 | L | 直線を描画 |
| 四角形 | R | 四角形を描画 |
| 楕円 | E | 楕円/円を描画 |
| テキスト | T | フォントサイズ変更可能なテキストを追加 |
| 番号 | N | 番号付きステップを追加（①②③...） |
| 蛍光ペン | H | 半透明のハイライトペン |

### 効果ツール
| ツール | ショートカット | 説明 |
|--------|---------------|------|
| 塗りつぶし | F | 塗りつぶした四角形を描画 |
| モザイク | M | 機密情報をピクセル化 |
| ぼかし | B | 機密領域をぼかす |
| スポットライト | S | 選択領域以外を暗くする |
| 拡大鏡 | G | 特定の領域を拡大表示 |

### 画像操作
- **切り抜き**: 選択した領域に画像をトリミング
- **回転**: 画像を90°時計回りに回転
- **リサイズ**: 縦横比を維持して画像をスケーリング

### その他の機能
- **元に戻す/やり直し**: 完全な履歴サポート（Ctrl+Z / Ctrl+Y）
- **クリップボードにコピー**: 素早く共有（Ctrl+C）
- **ファイルに保存**: PNG または JPEG 形式（Ctrl+S）
- **カスタマイズ可能なホットキー**: お好みのキャプチャショートカットを選択
- **システムトレイ**: バックグラウンドで静かに動作
- **多言語対応**: 英語と日本語をサポート

## システム要件

- Windows 10/11（64ビット）
- .NET 8.0 ランタイム

## インストール

### 方法1: インストーラーをダウンロード
1. [最新リリース](https://github.com/fukuyori/snapnote/releases/latest) ページからインストーラー（`SnapNoteStudio_Setup_x.x.x.exe`）をダウンロード
2. インストーラーを実行し、画面の指示に従ってインストール

### 方法2: ソースからビルド

#### 必要なもの
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup](https://jrsoftware.org/isdownload.php)（インストーラー作成時のみ）
- Visual Studio 2022 または VS Code（オプション）

#### ビルド手順

```bash
# リポジトリをクローン
git clone https://github.com/fukuyori/snapnote.git
cd snapnote

# 依存関係を復元
dotnet restore

# ビルド
dotnet build -c Release

# 実行
dotnet run -c Release
```

#### 単一実行ファイルとして発行

```bash
dotnet publish -c Release

# 出力先: bin/Release/net8.0-windows/win-x64/publish/SnapNoteStudio.exe
```

#### インストーラーの作成

Inno Setup をインストールした状態で以下を実行します：

```bash
# 1. Release ビルドを発行
dotnet publish -c Release

# 2. インストーラーを作成
iscc installer.iss

# 出力先: installer_output/SnapNoteStudio_Setup_x.x.x.exe
```

## 使い方

### キャプチャを開始

1. **ホットキー**: `Ctrl+Shift+S`（デフォルト）を押してキャプチャモードを開始
2. **システムトレイ**: トレイアイコンをダブルクリック、または右クリックして「キャプチャ」を選択

### キャプチャモード

1. クリック＆ドラッグでキャプチャしたい領域を選択
2. マウスボタンを離すとエディターが開く
3. `Escape`キーでキャンセル

### エディター

1. 左サイドバーで注釈ツールを選択
2. 上部ツールバーで色、太さ、濃さを調整
3. 画像上に注釈を描画
4. `Ctrl+Z`で元に戻す、`Ctrl+Y`でやり直し
5. 「コピー」をクリックまたは`Ctrl+C`でクリップボードにコピー
6. 「保存」をクリックまたは`Ctrl+S`でファイルに保存

### 設定

システムトレイアイコンを右クリックして「設定」を選択すると、設定画面が開きます。

| 設定項目 | 説明 | 初期値 |
|----------|------|--------|
| Language（言語） | 表示言語を切り替えます。English / 日本語 / 简体中文 / Español / 한국어 に対応 | English |
| Capture hotkey（キャプチャホットキー） | スクリーンキャプチャを開始するショートカットキーを選択します。PrintScreen / Ctrl+PrintScreen / Alt+PrintScreen / Ctrl+Shift+S / Ctrl+Shift+C / Ctrl+Alt+S / F12 / Ctrl+F12 から選択可能 | Ctrl+Shift+S |
| Start with Windows（Windows起動時に自動起動） | チェックを入れると、Windows起動時にSnapNote Studioを自動的に起動します。レジストリ（HKCU）に登録されます | OFF |
| Thickness（線の太さ） | 描画ツールのデフォルトの線の太さを設定します（1〜10） | 3 |
| Opacity（不透明度） | 描画ツールのデフォルトの不透明度を設定します（10%〜100%） | 100% |

## キーボードショートカット

### グローバル
| ショートカット | 動作 |
|----------------|------|
| Ctrl+Shift+S | キャプチャ開始（デフォルト、設定で変更可能） |

### エディター
| ショートカット | 動作 |
|----------------|------|
| Ctrl+Z | 元に戻す |
| Ctrl+Y | やり直し |
| Ctrl+C | クリップボードにコピー |
| Ctrl+S | ファイルに保存 |
| Delete | 選択した注釈を削除 |
| Escape | 選択解除 / 切り抜きモードをキャンセル |
| V, A, L, R, E, T, N, H, F, M, B, S, G | ツールショートカット |

## 設定ファイル

設定は以下の場所に保存されます：
```
%APPDATA%\SnapNoteStudio\settings.json
```

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照してください。

## 貢献

貢献は歓迎します！お気軽にPull Requestを送ってください。

## 謝辞

- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) - システムトレイ機能
- .NET 8.0 と WPF で構築
