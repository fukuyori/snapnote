using System.Windows;
using System.Windows.Controls;
using SnapNoteStudio.Services;

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
        
        LoadSettings();
        ApplyLocalization();
        
        DefaultStrokeSlider.ValueChanged += (s, e) => 
            DefaultStrokeText.Text = ((int)e.NewValue).ToString();
        DefaultOpacitySlider.ValueChanged += (s, e) => 
            DefaultOpacityText.Text = $"{(int)(e.NewValue * 100)}%";
    }
    
    private void ApplyLocalization()
    {
        Title = L10n.Get("SettingsTitle");
        TitleText.Text = L10n.Get("SettingsTitle");
        LanguageLabel.Text = L10n.Get("Language");
        HotkeyLabel.Text = L10n.Get("CaptureHotkey");
        StartupCheckBox.Content = L10n.Get("StartWithWindows");
        DefaultSettingsGroup.Header = L10n.Get("DefaultSettings");
        ThicknessLabel.Text = L10n.Get("DefaultThickness");
        OpacityLabel.Text = L10n.Get("DefaultOpacity");
        CancelButton.Content = L10n.Get("Cancel");
        SaveButton.Content = L10n.Get("Save");
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
        
        // Defaults
        DefaultStrokeSlider.Value = _settingsService.Settings.DefaultStrokeWidth;
        DefaultOpacitySlider.Value = _settingsService.Settings.DefaultOpacity;
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
        
        // Save defaults
        _settingsService.Settings.DefaultStrokeWidth = DefaultStrokeSlider.Value;
        _settingsService.Settings.DefaultOpacity = DefaultOpacitySlider.Value;
        
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
}
