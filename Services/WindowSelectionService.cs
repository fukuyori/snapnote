using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace SnapNoteStudio.Services;

public static class WindowSelectionService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public static Rect? GetWindowRectUnderCursor(IntPtr excludedWindowHandle)
    {
        var cursor = WinForms.Cursor.Position;
        var best = EnumerateWindows()
            .Where(window => window.Handle != excludedWindowHandle)
            .Where(window => !IsOwnedByCurrentProcess(window.Handle))
            .Where(window => Contains(window.HitTestRect, cursor.X, cursor.Y))
            .FirstOrDefault();

        if (best.Handle == IntPtr.Zero)
            return null;

        var virtualScreen = WinForms.SystemInformation.VirtualScreen;
        return new Rect(
            best.CaptureRect.Left - virtualScreen.Left,
            best.CaptureRect.Top - virtualScreen.Top,
            best.CaptureRect.Width,
            best.CaptureRect.Height);
    }

    private static IEnumerable<WindowInfo> EnumerateWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsIconic(handle))
                return true;

            if (!GetWindowRect(handle, out var rect))
                return true;

            var captureRect = TryGetExtendedFrameBounds(handle, out var extendedFrameRect)
                ? extendedFrameRect
                : rect;

            if (captureRect.Width <= 5 || captureRect.Height <= 5)
                return true;

            if ((GetWindowLong(handle, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0)
                return true;

            windows.Add(new WindowInfo(handle, rect, captureRect));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool TryGetExtendedFrameBounds(IntPtr handle, out NativeRect rect)
    {
        var result = DwmGetWindowAttribute(
            handle,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            out rect,
            Marshal.SizeOf<NativeRect>());

        return result == 0 && rect.Width > 5 && rect.Height > 5;
    }

    private static bool IsOwnedByCurrentProcess(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out var processId);
        return processId == Environment.ProcessId;
    }

    private static bool Contains(NativeRect rect, int x, int y)
    {
        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    private readonly record struct WindowInfo(IntPtr Handle, NativeRect HitTestRect, NativeRect CaptureRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);
}
