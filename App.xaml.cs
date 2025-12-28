using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using SnapNoteStudio.Services;
using SnapNoteStudio.Views;

namespace SnapNoteStudio;

public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private HotkeyService? _hotkeyService;
    private OverlayWindow? _overlayWindow;
    private SettingsService _settingsService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load settings (also initializes language)
        _settingsService.Load();

        // Create system tray icon
        _notifyIcon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(
                GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico")).Stream),
            ToolTipText = $"SnapNote Studio\n{_settingsService.Settings.CaptureHotkey}",
            ContextMenu = CreateContextMenu()
        };
        _notifyIcon.TrayMouseDoubleClick += (s, e) => StartCapture();

        // Register global hotkey
        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        
        RegisterHotkeyFromSettings();

        // Show startup notification
        _notifyIcon.ShowBalloonTip(
            L10n.Get("AppTitle"),
            string.Format(L10n.Get("AppStarted"), _settingsService.Settings.CaptureHotkey),
            BalloonIcon.Info);
    }

    private void RegisterHotkeyFromSettings()
    {
        _hotkeyService?.UnregisterHotkey();
        
        var (modifiers, key) = ParseHotkey(_settingsService.Settings.CaptureHotkey);
        
        if (!_hotkeyService!.RegisterHotkey(modifiers, key))
        {
            MessageBox.Show(
                string.Format(L10n.Get("HotkeyFailed"), _settingsService.Settings.CaptureHotkey),
                L10n.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        
        if (_notifyIcon != null)
        {
            _notifyIcon.ToolTipText = $"SnapNote Studio\n{_settingsService.Settings.CaptureHotkey}";
        }
    }

    private (System.Windows.Input.ModifierKeys, System.Windows.Input.Key) ParseHotkey(string hotkey)
    {
        var modifiers = System.Windows.Input.ModifierKeys.None;
        var key = System.Windows.Input.Key.PrintScreen;
        
        if (hotkey.Contains("Ctrl"))
            modifiers |= System.Windows.Input.ModifierKeys.Control;
        if (hotkey.Contains("Alt"))
            modifiers |= System.Windows.Input.ModifierKeys.Alt;
        if (hotkey.Contains("Shift"))
            modifiers |= System.Windows.Input.ModifierKeys.Shift;
        
        if (hotkey.Contains("PrintScreen"))
            key = System.Windows.Input.Key.PrintScreen;
        else if (hotkey.Contains("F12"))
            key = System.Windows.Input.Key.F12;
        else if (hotkey.EndsWith("+S"))
            key = System.Windows.Input.Key.S;
        else if (hotkey.EndsWith("+C"))
            key = System.Windows.Input.Key.C;
        
        return (modifiers, key);
    }

    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var captureItem = new System.Windows.Controls.MenuItem { Header = L10n.Get("Capture") };
        captureItem.Click += (s, e) => StartCapture();
        menu.Items.Add(captureItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var settingsItem = new System.Windows.Controls.MenuItem { Header = L10n.Get("Settings") };
        settingsItem.Click += (s, e) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = L10n.Get("Exit") };
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowSettings()
    {
        var dialog = new SettingsDialog(_settingsService);
        if (dialog.ShowDialog() == true)
        {
            // Recreate context menu with new language
            _notifyIcon!.ContextMenu = CreateContextMenu();
            
            if (dialog.SettingsChanged)
            {
                RegisterHotkeyFromSettings();
            }
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(StartCapture);
    }

    private void StartCapture()
    {
        if (_overlayWindow != null && _overlayWindow.IsVisible)
            return;

        // Capture screen BEFORE showing overlay
        var screenCapture = ScreenCaptureService.CaptureScreen();
        
        if (screenCapture == null)
        {
            MessageBox.Show(
                L10n.Get("CaptureFailed"),
                L10n.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        
        _overlayWindow = new OverlayWindow(screenCapture);
        _overlayWindow.CaptureCompleted += OnCaptureCompleted;
        _overlayWindow.CaptureCancelled += OnCaptureCancelled;
        _overlayWindow.Show();
    }

    private void OnCaptureCompleted(object? sender, CaptureEventArgs e)
    {
        _overlayWindow?.Close();
        _overlayWindow = null;

        if (e.CapturedImage != null && e.CapturedImage.PixelWidth > 0 && e.CapturedImage.PixelHeight > 0)
        {
            var editorWindow = new EditorWindow(e.CapturedImage, e.CaptureRegion, _settingsService);
            editorWindow.Show();
            editorWindow.Activate();
        }
    }

    private void OnCaptureCancelled(object? sender, EventArgs e)
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
    }

    private void ExitApplication()
    {
        _hotkeyService?.UnregisterHotkey();
        _notifyIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.UnregisterHotkey();
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
