using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using SnapNoteStudio.Services;
using SnapNoteStudio.Views;

namespace SnapNoteStudio;

public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private HotkeyService? _hotkeyService;
    private OverlayWindow? _overlayWindow;
    private EditorWindow? _reuseTargetWindow;
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
        _notifyIcon.TrayMouseDoubleClick += (s, e) => StartCapture("TrayDoubleClick");
        _notifyIcon.PreviewTrayToolTipOpen += OnPreviewTrayToolTipOpen;

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
        captureItem.Click += (s, e) => StartCapture("TrayMenu");
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
        // Return from the low-level keyboard hook immediately. Screen capture and window creation
        // must run later on the dispatcher or Windows can remove a hook that takes too long.
        var hookDelayMs = _hotkeyService?.LastHookDelayMs ?? 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Dispatcher.BeginInvoke(new Action(() => StartCapture("Hotkey", stopwatch, hookDelayMs)));
    }

    private void StartCapture(
        string trigger,
        System.Diagnostics.Stopwatch? stopwatch = null,
        uint? hookDelayMs = null)
    {
        stopwatch ??= System.Diagnostics.Stopwatch.StartNew();
        var dispatchMs = stopwatch.ElapsedMilliseconds;
        var timing = $"Capture trigger={trigger}"
            + (hookDelayMs is { } hookDelay ? $" hookDelay={hookDelay}ms" : string.Empty)
            + $" dispatch={dispatchMs}ms";

        if (_overlayWindow != null && _overlayWindow.IsVisible)
        {
            DiagnosticLog.Write($"{timing} ignored=overlay-visible");
            return;
        }

        // Capture screen BEFORE showing overlay
        var captureStartMs = stopwatch.ElapsedMilliseconds;
        var screenCapture = ScreenCaptureService.CaptureScreen(out var screenBounds);
        var captureMs = stopwatch.ElapsedMilliseconds - captureStartMs;
        timing += $" capture={captureMs}ms";

        if (screenCapture == null)
        {
            DiagnosticLog.Write($"{timing} result=capture-failed");
            MessageBox.Show(
                L10n.Get("CaptureFailed"),
                L10n.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        
        var createStartMs = stopwatch.ElapsedMilliseconds;
        _overlayWindow = new OverlayWindow(screenCapture, screenBounds, GetLastCaptureRegion());
        _overlayWindow.CaptureCompleted += OnCaptureCompleted;
        _overlayWindow.CaptureCancelled += OnCaptureCancelled;
        var showStartMs = stopwatch.ElapsedMilliseconds;
        timing += $" create={showStartMs - createStartMs}ms";

        var overlay = _overlayWindow;
        void OnOverlayContentRendered(object? sender, EventArgs args)
        {
            overlay.ContentRendered -= OnOverlayContentRendered;
            DiagnosticLog.Write(
                $"{timing} rendered={stopwatch.ElapsedMilliseconds - showStartMs}ms total={stopwatch.ElapsedMilliseconds}ms");
        }
        overlay.ContentRendered += OnOverlayContentRendered;

        overlay.Show();
    }

    private void OnPreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
    {
        // Keep ToolTipText as the notification-area identification string, but do not
        // let Hardcodet open its WPF tooltip popup over the desktop or a capture.
        e.Handled = true;
        DiagnosticLog.Write("TrayToolTip suppressed");
    }

    private void OnCaptureCompleted(object? sender, CaptureEventArgs e)
    {
        _overlayWindow?.Close();
        _overlayWindow = null;

        if (e.CapturedImage != null && e.CapturedImage.PixelWidth > 0 && e.CapturedImage.PixelHeight > 0)
        {
            try
            {
                Clipboard.SetImage(e.CapturedImage);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"Capture clipboard copy failed: {ex}");
                _notifyIcon?.ShowBalloonTip(
                    L10n.Get("AppTitle"),
                    $"{L10n.Get("CopyFailed")}: {ex.Message}",
                    BalloonIcon.Warning);
            }

            SaveLastCaptureRegion(e.CaptureRegion);

            if (_reuseTargetWindow != null && _reuseTargetWindow.TryLoadCapture(e.CapturedImage, e.CaptureRegion))
            {
                _reuseTargetWindow.Activate();
                return;
            }

            var editorWindow = new EditorWindow(e.CapturedImage, e.CaptureRegion, _settingsService);
            editorWindow.ReuseForNewCaptureChanged += OnEditorReuseForNewCaptureChanged;
            editorWindow.Closed += (s, args) =>
            {
                if (_reuseTargetWindow == editorWindow)
                    _reuseTargetWindow = null;
            };

            if (_settingsService.Settings.ReuseEditorWindowForNewCapture)
            {
                editorWindow.SetReuseForNewCapture(true);
                _reuseTargetWindow = editorWindow;
            }

            editorWindow.Show();
            editorWindow.Activate();
        }
    }

    private void OnEditorReuseForNewCaptureChanged(object? sender, bool enabled)
    {
        if (sender is not EditorWindow window)
            return;

        if (enabled)
        {
            if (_reuseTargetWindow != null && _reuseTargetWindow != window)
                _reuseTargetWindow.SetReuseForNewCapture(false);

            _reuseTargetWindow = window;
        }
        else if (_reuseTargetWindow == window)
        {
            _reuseTargetWindow = null;
        }

        _settingsService.Settings.ReuseEditorWindowForNewCapture = enabled;
        _settingsService.Save();
    }

    private Rect? GetLastCaptureRegion()
    {
        var region = _settingsService.Settings.LastCaptureRegion;
        if (region == null || !region.IsPhysicalScreenCoordinates || region.Width <= 5 || region.Height <= 5)
            return null;

        return new Rect(region.X, region.Y, region.Width, region.Height);
    }

    private void SaveLastCaptureRegion(Rect region)
    {
        if (region.Width <= 5 || region.Height <= 5)
            return;

        _settingsService.Settings.LastCaptureRegion = new SavedCaptureRegion
        {
            IsPhysicalScreenCoordinates = true,
            X = region.X,
            Y = region.Y,
            Width = region.Width,
            Height = region.Height
        };
        _settingsService.Save();
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
