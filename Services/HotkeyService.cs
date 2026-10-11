using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SnapNoteStudio.Services;

public class HotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hook;
    private uint _virtualKey;
    private ModifierKeys _modifiers;
    private bool _hotkeyDown;

    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Milliseconds between the key event timestamp and the hook callback for the last hotkey press.
    /// </summary>
    public uint LastHookDelayMs { get; private set; }

    public HotkeyService()
    {
        _hookProc = HookCallback;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    public bool RegisterHotkey(ModifierKeys modifiers, Key key)
    {
        UnregisterHotkey();

        _modifiers = modifiers;
        _virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _hook = SetWindowsHookExW(WhKeyboardLl, _hookProc, GetModuleHandleW(null), 0);
        return _hook != IntPtr.Zero;
    }

    public void UnregisterHotkey()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _hotkeyDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var keyInfo = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

            if (keyInfo.VirtualKeyCode == _virtualKey)
            {
                if (message is WmKeyDown or WmSysKeyDown && CurrentModifiers() == _modifiers)
                {
                    if (!_hotkeyDown)
                    {
                        _hotkeyDown = true;
                        // Both values are GetTickCount-based milliseconds; unsigned subtraction handles wraparound.
                        LastHookDelayMs = unchecked((uint)Environment.TickCount - keyInfo.Time);
                        try
                        {
                            HotkeyPressed?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            // Exceptions must not escape an unmanaged hook callback.
                        }
                    }

                    return new IntPtr(1);
                }

                if (message is WmKeyUp or WmSysKeyUp && _hotkeyDown)
                {
                    _hotkeyDown = false;
                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static ModifierKeys CurrentModifiers()
    {
        var modifiers = ModifierKeys.None;

        if (IsKeyDown(VkShift))
            modifiers |= ModifierKeys.Shift;
        if (IsKeyDown(VkControl))
            modifiers |= ModifierKeys.Control;
        if (IsKeyDown(VkMenu))
            modifiers |= ModifierKeys.Alt;
        if (IsKeyDown(VkLWin) || IsKeyDown(VkRWin))
            modifiers |= ModifierKeys.Windows;

        return modifiers;
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public void Dispose()
    {
        UnregisterHotkey();
    }
}
