# キャプチャ座標の検証

## 座標の扱い

- 選択開始点・終了点は `GetPhysicalCursorPos` で取得する画面上の実ピクセル座標。
- オーバーレイの HWND は `SetWindowPos` でキャプチャ時の仮想画面範囲に配置する。画面座標を WPF の `Left` / `Top` にそのまま代入しない。
- 選択枠だけを `OverlayCanvas.PointFromScreen` で WPF の座標に変換する。
- 切り抜きでは、実ピクセル座標からキャプチャ時の仮想画面の左上座標を引く。画面全体のサイズ比や表示倍率は掛けない。
- タイマー撮影では、撮影し直した画像と同時に取得した仮想画面範囲を使う。
- 保存する前回範囲は実ピクセル座標。`IsPhysicalScreenCoordinates` がない旧形式は当時の DPI を復元できないため、新規キャプチャが行われるまで再利用しない。

## 自動検証

PowerShell で実行する。

```powershell
.\scripts\test-capture-coordinates.ps1
.\scripts\test-capture-coordinates.ps1 -Live
```

通常のテストは負座標、画面境界、逆方向のドラッグ、画像の端、実際の切り抜き画素、保存形式を検証する。

`-Live` は実際の `OverlayWindow` のコードと XAML から透明なテストウインドウを開く。一時的にフォーカスが移る場合がある。マウスの移動、デスクトップの撮影、クリップボード操作、アプリ設定の保存は行わない。

各画面の位置で選択枠・生成画像の切り抜き・前回範囲を検証する。また、テストウインドウだけに `WM_DPICHANGED` を送り、125%・150%・200%・100%の WPF 座標変換を検証する。OS の表示設定は変更しない。

2026-10-25 の実行では合計68件が成功した。4画面の位置と模擬 DPI 変更を確認したが、テストウインドウを各画面へ移動しても `GetDpiForWindow` は96を返し、自然な DPI 変更通知は観測しなかった。模擬通知による成功を、実際の倍率変更や手動ドラッグの確認として扱わない。

## 手動確認とログ

実際に異なる表示倍率を設定した画面で、小さな目印を囲む範囲選択、画面をまたぐ選択、`Space` による前回範囲の再利用、タイマー撮影を確認する。

`%APPDATA%\SnapNoteStudio\logs\diagnostics.log` の `CaptureCoordinates` 行に、仮想画面範囲、HWND の位置・サイズ、WPF の DPI、選択開始・終了座標、切り抜き座標を記録する。キャプチャ画像や OCR テキストは記録しない。

## 参照した仕様

- [Microsoft: GetPhysicalCursorPos](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/winuser/nf-winuser-getphysicalcursorpos.md)
- [Microsoft: SetWindowPos](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/winuser/nf-winuser-setwindowpos.md)
- [Microsoft: WM_DPICHANGED](https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/hidpi/wm-dpichanged.md)
- [WPF: Window.OnDpiChanged の実装](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs)
