namespace SnapNoteStudio.Services;

public enum AppLanguage
{
    English,
    Japanese
}

public static class L10n
{
    private static AppLanguage _currentLanguage = AppLanguage.English;
    
    public static AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set => _currentLanguage = value;
    }
    
    public static string Get(string key)
    {
        return _currentLanguage == AppLanguage.Japanese 
            ? GetJapanese(key) 
            : GetEnglish(key);
    }
    
    private static string GetEnglish(string key) => key switch
    {
        // App
        "AppTitle" => "SnapNote Studio",
        "AppStarted" => "Started. Press {0} to capture.",
        "HotkeyFailed" => "Failed to register hotkey ({0}).\nIt may be used by another application.",
        "CaptureFailed" => "Screen capture failed.",
        
        // Tray Menu
        "Capture" => "Capture (_C)",
        "Settings" => "Settings (_S)",
        "Exit" => "Exit (_X)",
        
        // Editor Window
        "EditorTitle" => "SnapNote Studio - Edit",
        "Undo" => "↩ Undo",
        "Redo" => "↪ Redo",
        "Copy" => "📋 Copy",
        "Save" => "💾 Save",
        "Color" => "Color:",
        "Thickness" => "Thickness:",
        "Opacity" => "Opacity:",
        
        // Tool Groups
        "Drawing" => "Draw",
        "Effects" => "Effects",
        "Image" => "Image",
        
        // Tools
        "ToolSelect" => "Select (V)",
        "ToolArrow" => "Arrow (A)",
        "ToolLine" => "Line (L)",
        "ToolRect" => "Rectangle (R)",
        "ToolEllipse" => "Ellipse (E)",
        "ToolText" => "Text (T)",
        "ToolStep" => "Number (N)",
        "ToolHighlighter" => "Highlighter (H)",
        "ToolFilled" => "Fill (F)",
        "ToolMosaic" => "Mosaic (M)",
        "ToolBlur" => "Blur (B)",
        "ToolSpotlight" => "Spotlight (S)",
        "ToolMagnifier" => "Magnifier (G)",
        "ToolCrop" => "✂Crop",
        "ToolRotate" => "↻Rotate",
        "ToolResize" => "⇲Resize",
        
        // Status
        "Ready" => "Ready",
        "Size" => "Size: {0} × {1} px",
        "NextStep" => "Next step: {0}",
        "CopiedToClipboard" => "Copied to clipboard",
        "Saved" => "Saved: {0}",
        "CopyFailed" => "Copy failed: {0}",
        "SaveFailed" => "Save failed: {0}",
        
        // Dialogs
        "EnterText" => "Enter text:",
        "FontSize" => "Font size:",
        "Cancel" => "Cancel",
        "OK" => "OK",
        
        // Crop
        "CropInstruction" => "Crop: Drag to select area",
        "CropConfirm" => "Crop this area?",
        "CropTitle" => "Confirm Crop",
        
        // Resize Dialog
        "ResizeTitle" => "Resize",
        "NewSizeInstruction" => "Enter new size:",
        "Width" => "Width:",
        "Height" => "Height:",
        "KeepAspectRatio" => "Keep aspect ratio",
        "OriginalSize" => "Original size: {0} × {1} px",
        "InvalidNumber" => "Please enter valid numbers",
        
        // Settings Dialog
        "SettingsTitle" => "Settings",
        "CaptureHotkey" => "Capture hotkey:",
        "StartWithWindows" => "Start with Windows",
        "DefaultSettings" => "Default Settings",
        "DefaultThickness" => "Thickness:",
        "DefaultOpacity" => "Opacity:",
        "Language" => "Language:",
        "English" => "English",
        "Japanese" => "日本語",
        "RestartRequired" => "Language change will take effect after restart.",
        
        // Save Dialog
        "PngImage" => "PNG Image",
        "JpegImage" => "JPEG Image",
        "AllFiles" => "All Files",
        
        _ => key
    };
    
    private static string GetJapanese(string key) => key switch
    {
        // App
        "AppTitle" => "SnapNote Studio",
        "AppStarted" => "起動しました。{0} でキャプチャを開始できます。",
        "HotkeyFailed" => "ホットキー ({0}) の登録に失敗しました。\n他のアプリケーションで使用されている可能性があります。",
        "CaptureFailed" => "スクリーンキャプチャに失敗しました。",
        
        // Tray Menu
        "Capture" => "キャプチャ (_C)",
        "Settings" => "設定 (_S)",
        "Exit" => "終了 (_X)",
        
        // Editor Window
        "EditorTitle" => "SnapNote Studio - 編集",
        "Undo" => "↩ 戻す",
        "Redo" => "↪ やり直し",
        "Copy" => "📋 コピー",
        "Save" => "💾 保存",
        "Color" => "色:",
        "Thickness" => "太さ:",
        "Opacity" => "濃さ:",
        
        // Tool Groups
        "Drawing" => "描画",
        "Effects" => "効果",
        "Image" => "画像",
        
        // Tools
        "ToolSelect" => "選択 (V)",
        "ToolArrow" => "矢印 (A)",
        "ToolLine" => "線 (L)",
        "ToolRect" => "四角形 (R)",
        "ToolEllipse" => "楕円 (E)",
        "ToolText" => "テキスト (T)",
        "ToolStep" => "番号 (N)",
        "ToolHighlighter" => "蛍光ペン (H)",
        "ToolFilled" => "塗りつぶし (F)",
        "ToolMosaic" => "モザイク (M)",
        "ToolBlur" => "ぼかし (B)",
        "ToolSpotlight" => "スポットライト (S)",
        "ToolMagnifier" => "拡大鏡 (G)",
        "ToolCrop" => "✂切抜",
        "ToolRotate" => "↻回転",
        "ToolResize" => "⇲縮小",
        
        // Status
        "Ready" => "準備完了",
        "Size" => "サイズ: {0} × {1} px",
        "NextStep" => "次のステップ: {0}",
        "CopiedToClipboard" => "クリップボードにコピーしました",
        "Saved" => "保存しました: {0}",
        "CopyFailed" => "コピーに失敗しました: {0}",
        "SaveFailed" => "保存に失敗しました: {0}",
        
        // Dialogs
        "EnterText" => "テキストを入力してください:",
        "FontSize" => "フォントサイズ:",
        "Cancel" => "キャンセル",
        "OK" => "OK",
        
        // Crop
        "CropInstruction" => "切り抜き: ドラッグで範囲を選択してください",
        "CropConfirm" => "この範囲で切り抜きますか？",
        "CropTitle" => "切り抜き確認",
        
        // Resize Dialog
        "ResizeTitle" => "サイズ変更",
        "NewSizeInstruction" => "新しいサイズを入力してください:",
        "Width" => "幅:",
        "Height" => "高さ:",
        "KeepAspectRatio" => "縦横比を維持",
        "OriginalSize" => "元のサイズ: {0} × {1} px",
        "InvalidNumber" => "有効な数値を入力してください",
        
        // Settings Dialog
        "SettingsTitle" => "設定",
        "CaptureHotkey" => "キャプチャショートカット:",
        "StartWithWindows" => "Windows起動時に自動起動する",
        "DefaultSettings" => "デフォルト設定",
        "DefaultThickness" => "太さ:",
        "DefaultOpacity" => "濃さ:",
        "Language" => "言語:",
        "English" => "English",
        "Japanese" => "日本語",
        "RestartRequired" => "言語の変更は再起動後に反映されます。",
        
        // Save Dialog
        "PngImage" => "PNG画像",
        "JpegImage" => "JPEG画像",
        "AllFiles" => "すべてのファイル",
        
        _ => key
    };
}
