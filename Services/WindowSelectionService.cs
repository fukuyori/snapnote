using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WinForms = System.Windows.Forms;

namespace SnapNoteStudio.Services;

public static class WindowSelectionService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;
    private const int MaxLoggedTextLength = 200;
    private static readonly object LogSync = new();
    private static readonly HashSet<string> WindowsShellSurfaceProcesses = new(
        ["TextInputHost", "StartMenuExperienceHost", "SearchHost"],
        StringComparer.OrdinalIgnoreCase);

    public static string DiagnosticLogPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnapNoteStudio",
        "logs",
        "window-selection.log");

    public static Rect? GetWindowRectUnderCursor(IntPtr excludedWindowHandle)
    {
        var cursor = WinForms.Cursor.Position;
        var virtualScreen = WinForms.SystemInformation.VirtualScreen;
        var candidates = EnumerateWindowsAtPoint(cursor.X, cursor.Y, excludedWindowHandle);
        var best = candidates.FirstOrDefault(window => window.IsSelectable);

        Rect? result = best.Handle == IntPtr.Zero
            ? null
            : new Rect(
                best.CaptureRect.Left - virtualScreen.Left,
                best.CaptureRect.Top - virtualScreen.Top,
                best.CaptureRect.Width,
                best.CaptureRect.Height);

        WriteDiagnosticLog(cursor.X, cursor.Y, excludedWindowHandle, virtualScreen, candidates, best, result);
        return result;
    }

    private static IReadOnlyList<WindowInfo> EnumerateWindowsAtPoint(
        int cursorX,
        int cursorY,
        IntPtr excludedWindowHandle)
    {
        var windows = new List<WindowInfo>();
        var zOrder = 0;

        EnumWindows((handle, _) =>
        {
            var currentZOrder = zOrder++;

            if (!GetWindowRect(handle, out var hitTestRect)
                || !Contains(hitTestRect, cursorX, cursorY))
            {
                return true;
            }

            var visible = IsWindowVisible(handle);
            var minimized = IsIconic(handle);
            var extendedStyle = GetWindowLong(handle, GWL_EXSTYLE);
            GetWindowThreadProcessId(handle, out var processId);
            var processName = GetProcessName(processId);
            var className = GetWindowClassName(handle);
            var title = GetWindowTitle(handle);
            var isDwmCloaked = IsDwmCloaked(handle);

            var captureRect = TryGetExtendedFrameBounds(handle, out var extendedFrameRect)
                ? extendedFrameRect
                : hitTestRect;

            var exclusionReason = GetExclusionReason(
                handle,
                excludedWindowHandle,
                processId,
                processName,
                className,
                visible,
                minimized,
                isDwmCloaked,
                extendedStyle,
                captureRect);

            windows.Add(new WindowInfo(
                currentZOrder,
                handle,
                processId,
                processName,
                className,
                title,
                hitTestRect,
                captureRect,
                extendedStyle,
                isDwmCloaked,
                exclusionReason == null,
                exclusionReason ?? "selected candidate"));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string? GetExclusionReason(
        IntPtr handle,
        IntPtr excludedWindowHandle,
        int processId,
        string processName,
        string className,
        bool visible,
        bool minimized,
        bool isDwmCloaked,
        int extendedStyle,
        NativeRect captureRect)
    {
        if (!visible)
            return "not visible";
        if (minimized)
            return "minimized";
        if (isDwmCloaked)
            return "DWM cloaked";
        if (captureRect.Width <= 5 || captureRect.Height <= 5)
            return "capture rectangle is too small";
        if (handle == excludedWindowHandle)
            return "capture overlay window";
        if (processId == Environment.ProcessId)
            return "owned by Snapnote process";
        if ((extendedStyle & WS_EX_TOOLWINDOW) != 0)
            return "WS_EX_TOOLWINDOW";
        if (IsWindowsShellFullScreenSurface(processName, className, extendedStyle, captureRect))
            return "Windows shell full-screen surface";

        return null;
    }

    private static bool IsWindowsShellFullScreenSurface(
        string processName,
        string className,
        int extendedStyle,
        NativeRect captureRect)
    {
        if (!WindowsShellSurfaceProcesses.Contains(processName)
            || !string.Equals(className, "Windows.UI.Core.CoreWindow", StringComparison.Ordinal)
            || (extendedStyle & WS_EX_NOREDIRECTIONBITMAP) == 0)
        {
            return false;
        }

        return WinForms.Screen.AllScreens.Any(screen => MatchesScreenBounds(captureRect, screen.Bounds));
    }

    private static bool MatchesScreenBounds(NativeRect rect, System.Drawing.Rectangle bounds)
    {
        const int tolerance = 2;
        return Math.Abs(rect.Left - bounds.Left) <= tolerance
            && Math.Abs(rect.Top - bounds.Top) <= tolerance
            && Math.Abs(rect.Right - bounds.Right) <= tolerance
            && Math.Abs(rect.Bottom - bounds.Bottom) <= tolerance;
    }

    private static bool IsDwmCloaked(IntPtr handle)
    {
        var result = DwmGetWindowAttributeInt(
            handle,
            DWMWA_CLOAKED,
            out var cloaked,
            sizeof(int));

        return result == 0 && cloaked != 0;
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

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static string GetWindowClassName(IntPtr handle)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(handle, buffer, buffer.Capacity) > 0
            ? SanitizeLogText(buffer.ToString())
            : "<unavailable>";
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var buffer = new StringBuilder(512);
        return GetWindowText(handle, buffer, buffer.Capacity) > 0
            ? SanitizeLogText(buffer.ToString())
            : string.Empty;
    }

    private static string SanitizeLogText(string text)
    {
        var sanitized = text.Replace("\r", "\\r").Replace("\n", "\\n");
        return sanitized.Length <= MaxLoggedTextLength
            ? sanitized
            : sanitized[..MaxLoggedTextLength] + "...";
    }

    private static void WriteDiagnosticLog(
        int cursorX,
        int cursorY,
        IntPtr excludedWindowHandle,
        System.Drawing.Rectangle virtualScreen,
        IReadOnlyList<WindowInfo> candidates,
        WindowInfo selected,
        Rect? result)
    {
        try
        {
            var log = new StringBuilder();
            log.AppendLine($"[{DateTimeOffset.Now:O}] Window selection");
            log.AppendLine($"Cursor=({cursorX},{cursorY}) ExcludedHandle=0x{excludedWindowHandle.ToInt64():X}");
            log.AppendLine(
                $"VirtualScreen=({virtualScreen.Left},{virtualScreen.Top}) "
                + $"{virtualScreen.Width}x{virtualScreen.Height}");

            if (candidates.Count == 0)
            {
                log.AppendLine("Candidates=<none>");
            }
            else
            {
                log.AppendLine("Candidates (front to back):");
                foreach (var candidate in candidates)
                {
                    log.AppendLine(
                        $"  Z={candidate.ZOrder} Handle=0x{candidate.Handle.ToInt64():X} "
                        + $"PID={candidate.ProcessId} Process=\"{candidate.ProcessName}\" "
                        + $"Class=\"{candidate.ClassName}\" Title=\"{candidate.Title}\" "
                        + $"HitRect={FormatRect(candidate.HitTestRect)} "
                        + $"CaptureRect={FormatRect(candidate.CaptureRect)} "
                        + $"ExStyle=0x{candidate.ExtendedStyle:X8} "
                        + $"DwmCloaked={candidate.IsDwmCloaked} "
                        + $"Selectable={candidate.IsSelectable} Reason=\"{candidate.Reason}\"");
                }
            }

            if (selected.Handle == IntPtr.Zero || result == null)
            {
                log.AppendLine("Selected=<none>");
            }
            else
            {
                log.AppendLine(
                    $"Selected=0x{selected.Handle.ToInt64():X} "
                    + $"ResultRect=({result.Value.X},{result.Value.Y}) "
                    + $"{result.Value.Width}x{result.Value.Height}");
            }

            log.AppendLine();

            lock (LogSync)
            {
                var directory = System.IO.Path.GetDirectoryName(DiagnosticLogPath);
                if (!string.IsNullOrEmpty(directory))
                    System.IO.Directory.CreateDirectory(directory);

                System.IO.File.AppendAllText(DiagnosticLogPath, log.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Window selection diagnostic log failed: {ex.Message}");
        }
    }

    private static string FormatRect(NativeRect rect)
    {
        return $"({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) {rect.Width}x{rect.Height}";
    }

    private static bool Contains(NativeRect rect, int x, int y)
    {
        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    private readonly record struct WindowInfo(
        int ZOrder,
        IntPtr Handle,
        int ProcessId,
        string ProcessName,
        string ClassName,
        string Title,
        NativeRect HitTestRect,
        NativeRect CaptureRect,
        int ExtendedStyle,
        bool IsDwmCloaked,
        bool IsSelectable,
        string Reason);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeInt(
        IntPtr hwnd,
        int dwAttribute,
        out int pvAttribute,
        int cbAttribute);
}
