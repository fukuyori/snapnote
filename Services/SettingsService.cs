using System.Text.Json;
using Microsoft.Win32;

namespace SnapNoteStudio.Services;

public class AppSettings
{
    public string CaptureHotkey { get; set; } = "Ctrl+Shift+S";
    public bool StartWithWindows { get; set; } = false;
    public double DefaultOpacity { get; set; } = 1.0;
    public double DefaultStrokeWidth { get; set; } = 3;
    public string DefaultColor { get; set; } = "#FFFF0000";
    public string Language { get; set; } = "English";
}

public class SettingsService
{
    private static readonly string SettingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnapNoteStudio", "settings.json");
    
    private const string AppName = "SnapNoteStudio";
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    
    public AppSettings Settings { get; private set; } = new();
    
    public void Load()
    {
        try
        {
            if (System.IO.File.Exists(SettingsPath))
            {
                var json = System.IO.File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
        
        // Apply language setting
        L10n.CurrentLanguage = Settings.Language switch
        {
            "Japanese" => AppLanguage.Japanese,
            "Chinese" => AppLanguage.Chinese,
            "Spanish" => AppLanguage.Spanish,
            "Korean" => AppLanguage.Korean,
            _ => AppLanguage.English
        };
    }
    
    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsPath, json);
            
            // Update Windows startup setting
            UpdateStartupRegistry();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
        }
    }
    
    private void UpdateStartupRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;
            
            if (Settings.StartWithWindows)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Registry update error: {ex.Message}");
        }
    }
    
    public static readonly Dictionary<string, string> AvailableHotkeys = new()
    {
        { "PrintScreen", "PrintScreen" },
        { "Ctrl+PrintScreen", "Ctrl+PrintScreen" },
        { "Alt+PrintScreen", "Alt+PrintScreen" },
        { "Ctrl+Shift+S", "Ctrl+Shift+S" },
        { "Ctrl+Shift+C", "Ctrl+Shift+C" },
        { "Ctrl+Alt+S", "Ctrl+Alt+S" },
        { "F12", "F12" },
        { "Ctrl+F12", "Ctrl+F12" }
    };
}
