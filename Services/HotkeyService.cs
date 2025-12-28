using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SnapNoteStudio.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public bool RegisterHotkey(ModifierKeys modifiers, Key key)
    {
        // Create a hidden window for receiving hotkey messages
        var parameters = new HwndSourceParameters("SnapNoteStudioHotkey")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0x800000 // WS_POPUP
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _windowHandle = _source.Handle;

        // Convert WPF modifiers to Win32 modifiers
        uint winModifiers = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt))
            winModifiers |= 0x0001; // MOD_ALT
        if (modifiers.HasFlag(ModifierKeys.Control))
            winModifiers |= 0x0002; // MOD_CONTROL
        if (modifiers.HasFlag(ModifierKeys.Shift))
            winModifiers |= 0x0004; // MOD_SHIFT
        if (modifiers.HasFlag(ModifierKeys.Windows))
            winModifiers |= 0x0008; // MOD_WIN

        // Convert WPF key to Win32 virtual key
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, winModifiers, vk);
        return _isRegistered;
    }

    public void UnregisterHotkey()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }

        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotkey();
    }
}
