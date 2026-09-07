using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapNoteStudio.Services;
using WinForms = System.Windows.Forms;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;

namespace SnapNoteStudio.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private string _initialLanguage;
    
    public bool SettingsChanged { get; private set; }
    
    public SettingsDialog(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _initialLanguage = settingsService.Settings.Language;
        VersionText.Text = $"SnapNote Studio {GetAppVersion()}";
        
        LoadSettings();
        ApplyLocalization();

        DefaultStrokeSlider.ValueChanged += (s, e) =>
            DefaultStrokeText.Text = ((int)e.NewValue).ToString();
        DefaultOpacitySlider.ValueChanged += (s, e) =>
            DefaultOpacityText.Text = $"{(int)(e.NewValue * 100)}%";

        _ = RefreshOllamaModelsAsync(showWarningIfEmpty: false);

        foreach (var box in ShortcutBoxes)
            box.PreviewKeyDown += ShortcutBox_PreviewKeyDown;
    }

    private TextBox[] ShortcutBoxes => new[]
    {
        CopyShortcutBox, SaveShortcutBox, OcrShortcutBox, TranslateShortcutBox, CloseShortcutBox
    };

    private static string GetAppVersion()
    {
        var version = typeof(SettingsDialog).Assembly.GetName().Version;
        return version == null ? string.Empty : version.ToString(3);
    }
    
    private void ApplyLocalization()
    {
        Title = L10n.Get("SettingsTitle");
        TitleText.Text = L10n.Get("SettingsTitle");
        LanguageLabel.Text = L10n.Get("Language");
        HotkeyLabel.Text = L10n.Get("CaptureHotkey");
        StartupCheckBox.Content = L10n.Get("StartWithWindows");
        SaveDirectoryLabel.Text = L10n.Get("SaveDirectory");
        BrowseButton.Content = L10n.Get("Browse");
        DefaultSettingsGroup.Header = L10n.Get("DefaultSettings");
        ThicknessLabel.Text = L10n.Get("DefaultThickness");
        OpacityLabel.Text = L10n.Get("DefaultOpacity");
        CancelButton.Content = L10n.Get("Cancel");
        SaveButton.Content = L10n.Get("Save");
        OcrLanguagesGroup.Header = L10n.Get("OcrLanguages");
        OcrLanguagesHint.Text = L10n.Get("OcrLanguagesHint");
        OllamaGroup.Header = L10n.Get("OllamaGroupTitle");
        OllamaHint.Text = L10n.Get("OllamaHint");
        OllamaEndpointLabel.Text = L10n.Get("OllamaEndpoint");
        OllamaModelLabel.Text = L10n.Get("OllamaModel");
        OllamaRefreshButton.Content = L10n.Get("Refresh");
        EditorShortcutsGroup.Header = L10n.Get("EditorShortcutsGroupTitle");
        EditorShortcutsHint.Text = L10n.Get("EditorShortcutsHint");
        ShortcutCopyLabel.Text = L10n.Get("ShortcutCopy");
        ShortcutSaveLabel.Text = L10n.Get("ShortcutSave");
        ShortcutOcrLabel.Text = L10n.Get("ShortcutOcr");
        ShortcutTranslateLabel.Text = L10n.Get("ShortcutTranslate");
        ShortcutCloseLabel.Text = L10n.Get("ShortcutClose");
    }
    
    private void LoadSettings()
    {
        // Language selection
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag?.ToString() == _settingsService.Settings.Language)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }
        if (LanguageComboBox.SelectedItem == null)
            LanguageComboBox.SelectedIndex = 0;
        
        // Hotkey options
        foreach (var hotkey in SettingsService.AvailableHotkeys)
        {
            var item = new ComboBoxItem { Content = hotkey.Value, Tag = hotkey.Key };
            HotkeyComboBox.Items.Add(item);
            
            if (hotkey.Key == _settingsService.Settings.CaptureHotkey)
            {
                HotkeyComboBox.SelectedItem = item;
            }
        }
        
        if (HotkeyComboBox.SelectedItem == null && HotkeyComboBox.Items.Count > 0)
        {
            HotkeyComboBox.SelectedIndex = 0;
        }
        
        // Startup
        StartupCheckBox.IsChecked = _settingsService.Settings.StartWithWindows;

        // Save location
        SaveDirectoryTextBox.Text = string.IsNullOrWhiteSpace(_settingsService.Settings.SaveDirectory)
            ? SettingsService.DefaultSaveDirectory
            : _settingsService.Settings.SaveDirectory;
        
        // Defaults
        DefaultStrokeSlider.Value = _settingsService.Settings.DefaultStrokeWidth;
        DefaultOpacitySlider.Value = _settingsService.Settings.DefaultOpacity;

        // OCR languages
        OcrLanguagesPanel.Children.Clear();
        var selectedTags = _settingsService.Settings.OcrLanguages;

        foreach (var language in OcrService.AvailableRecognizerLanguages)
        {
            var checkBox = new CheckBox
            {
                Content = $"{language.DisplayName} ({language.LanguageTag})",
                Tag = language.LanguageTag,
                IsChecked = selectedTags.Contains(language.LanguageTag),
                Margin = new Thickness(0, 2, 0, 2)
            };
            OcrLanguagesPanel.Children.Add(checkBox);
        }

        if (OcrLanguagesPanel.Children.Count == 0)
        {
            OcrLanguagesPanel.Children.Add(new TextBlock
            {
                Text = L10n.Get("OcrLanguagesNone"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Ollama translation settings
        OllamaEndpointTextBox.Text = _settingsService.Settings.OllamaEndpoint;
        OllamaModelComboBox.Text = _settingsService.Settings.OllamaModel;

        // Editor shortcuts
        var shortcuts = _settingsService.Settings.EditorShortcuts;
        CopyShortcutBox.Text = shortcuts.Copy;
        SaveShortcutBox.Text = shortcuts.Save;
        OcrShortcutBox.Text = shortcuts.Ocr;
        TranslateShortcutBox.Text = shortcuts.Translate;
        CloseShortcutBox.Text = shortcuts.Close;
    }

    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            MessageBox.Show(L10n.Get("ShortcutRequiresModifier"), L10n.Get("SettingsTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (modifiers == ModifierKeys.Control && key is Key.Z or Key.Y)
        {
            MessageBox.Show(L10n.Get("ShortcutReserved"), L10n.Get("SettingsTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var formatted = ShortcutUtilities.Format(modifiers, key);

        foreach (var otherBox in ShortcutBoxes)
        {
            if (otherBox != box && otherBox.Text == formatted)
            {
                MessageBox.Show(string.Format(L10n.Get("ShortcutConflict"), formatted), L10n.Get("SettingsTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        box.Text = formatted;
    }
    
    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            L10n.CurrentLanguage = lang switch
            {
                "Japanese" => AppLanguage.Japanese,
                "Chinese" => AppLanguage.Chinese,
                "Spanish" => AppLanguage.Spanish,
                "Korean" => AppLanguage.Korean,
                _ => AppLanguage.English
            };
            ApplyLocalization();
        }
    }
    
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Save language
        if (LanguageComboBox.SelectedItem is ComboBoxItem langItem && langItem.Tag is string lang)
        {
            if (_settingsService.Settings.Language != lang)
            {
                _settingsService.Settings.Language = lang;
                if (_initialLanguage != lang)
                {
                    MessageBox.Show(L10n.Get("RestartRequired"), 
                        L10n.Get("SettingsTitle"), 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        
        // Save hotkey
        if (HotkeyComboBox.SelectedItem is ComboBoxItem item && item.Tag is string hotkey)
        {
            if (_settingsService.Settings.CaptureHotkey != hotkey)
            {
                _settingsService.Settings.CaptureHotkey = hotkey;
                SettingsChanged = true;
            }
        }
        
        // Save startup
        _settingsService.Settings.StartWithWindows = StartupCheckBox.IsChecked == true;

        // Save location
        _settingsService.Settings.SaveDirectory = string.IsNullOrWhiteSpace(SaveDirectoryTextBox.Text)
            ? SettingsService.DefaultSaveDirectory
            : SaveDirectoryTextBox.Text.Trim();
        
        // Save defaults
        _settingsService.Settings.DefaultStrokeWidth = DefaultStrokeSlider.Value;
        _settingsService.Settings.DefaultOpacity = DefaultOpacitySlider.Value;

        // Save OCR languages
        _settingsService.Settings.OcrLanguages = OcrLanguagesPanel.Children
            .OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (string)cb.Tag)
            .ToList();

        // Save Ollama translation settings
        _settingsService.Settings.OllamaEndpoint = string.IsNullOrWhiteSpace(OllamaEndpointTextBox.Text)
            ? "http://localhost:11434"
            : OllamaEndpointTextBox.Text.Trim();
        _settingsService.Settings.OllamaModel = OllamaModelComboBox.Text.Trim();

        // Save editor shortcuts
        _settingsService.Settings.EditorShortcuts = new EditorShortcutSettings
        {
            Copy = CopyShortcutBox.Text,
            Save = SaveShortcutBox.Text,
            Ocr = OcrShortcutBox.Text,
            Translate = TranslateShortcutBox.Text,
            Close = CloseShortcutBox.Text
        };

        _settingsService.Save();
        
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Restore original language
        L10n.CurrentLanguage = _initialLanguage switch
        {
            "Japanese" => AppLanguage.Japanese,
            "Chinese" => AppLanguage.Chinese,
            "Spanish" => AppLanguage.Spanish,
            "Korean" => AppLanguage.Korean,
            _ => AppLanguage.English
        };
        
        DialogResult = false;
        Close();
    }

    private async void OllamaRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshOllamaModelsAsync(showWarningIfEmpty: true);
    }

    private async Task RefreshOllamaModelsAsync(bool showWarningIfEmpty)
    {
        var endpoint = OllamaEndpointTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        OllamaRefreshButton.IsEnabled = false;
        var currentText = OllamaModelComboBox.Text;

        try
        {
            var models = await OllamaTranslationService.GetAvailableModelsAsync(endpoint);

            OllamaModelComboBox.Items.Clear();
            foreach (var model in models)
                OllamaModelComboBox.Items.Add(new ComboBoxItem { Content = model });

            if (models.Count == 0 && showWarningIfEmpty)
            {
                MessageBox.Show(L10n.Get("OllamaNoModelsFound"), L10n.Get("SettingsTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            OllamaModelComboBox.Text = currentText;
        }
        finally
        {
            OllamaRefreshButton.IsEnabled = true;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = L10n.Get("SelectSaveDirectory"),
            SelectedPath = string.IsNullOrWhiteSpace(SaveDirectoryTextBox.Text)
                ? SettingsService.DefaultSaveDirectory
                : SaveDirectoryTextBox.Text,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            SaveDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }
}
