using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapNoteStudio.Services;
using SnapNoteStudio.Views;
using WinForms = System.Windows.Forms;

internal static class Program
{
    private static int _checks;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [STAThread]
    private static void Main(string[] args)
    {
        // Physical desktop from the reported 100%/150%/125%/100% arrangement.
        var desktop = new Rect(-1920, 0, 6400, 2720);
        var cases = new (string Name, Rect Selection, Int32Rect Expected)[]
        {
            ("Primary 100%", new Rect(100, 100, 400, 300), new Int32Rect(2020, 100, 400, 300)),
            ("Left 150%", new Rect(-1800, 200, 300, 150), new Int32Rect(120, 200, 300, 150)),
            ("Right 125%", new Rect(2800, 200, 250, 125), new Int32Rect(4720, 200, 250, 125)),
            ("Lower display", new Rect(500, 1600, 100, 200), new Int32Rect(2420, 1600, 100, 200)),
            ("Across display boundary", new Rect(-100, 200, 500, 300), new Int32Rect(1820, 200, 500, 300)),
            ("Reverse drag", new Rect(new Point(-1500, 700), new Point(-1800, 200)), new Int32Rect(120, 200, 300, 500)),
            ("Clip left/top", new Rect(-2000, -50, 200, 200), new Int32Rect(0, 0, 120, 150)),
            ("Clip right/bottom", new Rect(4400, 2700, 200, 200), new Int32Rect(6320, 2700, 80, 20)),
            ("Outside", new Rect(5000, 0, 200, 200), Int32Rect.Empty),
            ("Empty", Rect.Empty, Int32Rect.Empty),
            ("Zero width", new Rect(100, 100, 0, 100), Int32Rect.Empty)
        };
        foreach (var test in cases)
        {
            var actual = CaptureCoordinates.GetCropRect(test.Selection, desktop, 6400, 2720);
            Check(actual == test.Expected, test.Name);
            if (!actual.IsEmpty)
            {
                var restored = CaptureCoordinates.ToScreenRect(actual, desktop);
                var clipped = test.Selection;
                clipped.Intersect(desktop);
                Check(restored == clipped, test.Name + " saved region round trip");
            }
        }
        Check(CaptureCoordinates.GetCropRect(new Rect(-100, -500, 40, 60),
            new Rect(-200, -1080, 2120, 2520), 2120, 2520) == new Int32Rect(100, 580, 40, 60), "Negative top origin");
        Check(CaptureCoordinates.GetCropRect(new Rect(300, 200, 400, 300),
            new Rect(0, 0, 2560, 1440), 2560, 1440) == new Int32Rect(300, 200, 400, 300), "Single scaled display needs no DPI multiplier");

        var legacy = JsonSerializer.Deserialize<SavedCaptureRegion>("{\"X\":100,\"Y\":200,\"Width\":300,\"Height\":400}")!;
        Check(!legacy.IsPhysicalScreenCoordinates, "Legacy saved region is distinguishable");
        var saved = new SavedCaptureRegion { IsPhysicalScreenCoordinates = true, X = -1800, Y = 200, Width = 300, Height = 150 };
        var roundTrip = JsonSerializer.Deserialize<SavedCaptureRegion>(JsonSerializer.Serialize(saved))!;
        Check(roundTrip.IsPhysicalScreenCoordinates && roundTrip.X == -1800 && roundTrip.Width == 300, "Physical region JSON round trip");

        CheckCroppedPixels();
        if (args.Contains("--live")) CheckLiveOverlay();
        Console.WriteLine($"All {_checks} capture coordinate checks passed. Live overlay checks: {args.Contains("--live")}.");
    }

    private static void CheckCroppedPixels()
    {
        var source = MakeSource(600, 400);
        var desktop = new Rect(-400, -200, 600, 400);
        var crop = CaptureCoordinates.GetCropRect(new Rect(-300, -150, 125, 75), desktop, 600, 400);
        var result = new CroppedBitmap(source, crop);
        var pixels = new byte[125 * 75 * 4];
        result.CopyPixels(pixels, 125 * 4, 0);
        for (int y = 0; y < 75; y++)
            for (int x = 0; x < 125; x++)
            {
                int i = (y * 125 + x) * 4;
                if (pixels[i] != (byte)(x + 100) || pixels[i + 1] != (byte)(y + 50))
                    throw new Exception("Cropped pixel coordinates differ from selection");
            }
        Check(result.PixelWidth == 125 && result.PixelHeight == 75, "Crop size and every pixel match");
    }

    private static void CheckLiveOverlay()
    {
        // Explicit opt-in. Creates only test-owned windows; does not move the mouse,
        // capture the desktop, touch the clipboard, or save application settings.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var desktop = ScreenCaptureService.GetVirtualScreenBounds();
        var source = MakeSource((int)desktop.Width, (int)desktop.Height);
        var previous = new Rect(desktop.Left + 100, desktop.Top + 100, 200, 150);
        var overlay = new OverlayWindow(source, desktop, previous) { Opacity = 0, Topmost = false };
        try
        {
            overlay.Show();
            Pump();
            var hwnd = new WindowInteropHelper(overlay).Handle;
            Console.WriteLine($"Overlay DPI awareness={GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(hwnd))}, native DPI={GetDpiForWindow(hwnd)}");
            CheckWindowBounds(hwnd, desktop);
            var canvas = (Canvas)overlay.FindName("OverlayCanvas");
            foreach (var screen in WinForms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var selection = new Rect(bounds.Left + 30, bounds.Top + 40, 240, 160);
                SetField(overlay, "_startScreenPoint", selection.TopLeft);
                SetField(overlay, "_currentScreenPoint", selection.BottomRight);
                Invoke(overlay, "UpdateSelection");
                overlay.UpdateLayout();
                var border = (FrameworkElement)overlay.FindName("SelectionBorder");
                var topLeft = canvas.PointToScreen(new Point(Canvas.GetLeft(border), Canvas.GetTop(border)));
                var bottomRight = canvas.PointToScreen(new Point(Canvas.GetLeft(border) + border.Width, Canvas.GetTop(border) + border.Height));
                Check(Near(topLeft, selection.TopLeft) && Near(bottomRight, selection.BottomRight), $"Physical selection border {screen.DeviceName}");

                CaptureEventArgs? completed = null;
                EventHandler<CaptureEventArgs> handler = (_, e) => completed = e;
                overlay.CaptureCompleted += handler;
                Invoke(overlay, "CompleteCapture", selection);
                overlay.CaptureCompleted -= handler;
                Check(completed?.CaptureRegion == selection && completed.CapturedImage?.PixelWidth == 240
                    && completed.CapturedImage.PixelHeight == 160, $"Real overlay crop path {screen.DeviceName}");
                var firstPixel = new byte[4];
                completed!.CapturedImage!.CopyPixels(new Int32Rect(0, 0, 1, 1), firstPixel, 4, 0);
                Check(firstPixel[0] == (byte)(selection.X - desktop.X)
                    && firstPixel[1] == (byte)(selection.Y - desktop.Y), $"Actual crop origin {screen.DeviceName}");
                Console.WriteLine($"{screen.DeviceName}: screen={bounds}, selected={selection}, border={topLeft}..{bottomRight}");
            }

            Check((bool)Invoke(overlay, "ShowLastCaptureRegion")!, "Previous region preview");
            Check((Rect)Invoke(overlay, "GetSelectionScreenRect")! == previous, "Previous region remains physical");

            // Check native transitions if this desktop actually supplies different
            // window DPI values. Device scale recommendations alone are not proof.
            int transitions = 0;
            foreach (var screen in WinForms.Screen.AllScreens)
            {
                var beforeDpi = VisualTreeHelper.GetDpi(overlay);
                var bounds = screen.Bounds;
                Check(SetWindowPos(hwnd, IntPtr.Zero, bounds.Left + 50, bounds.Top + 50, 200, 200, 0x14), "Move test overlay");
                GetWindowRect(hwnd, out var movedRect);
                Console.WriteLine($"Requested move to {screen.DeviceName}: HWND=({movedRect.Left},{movedRect.Top},{movedRect.Right - movedRect.Left},{movedRect.Bottom - movedRect.Top}), DPI={GetDpiForWindow(hwnd)}");
                var movedDpi = VisualTreeHelper.GetDpi(overlay);
                Pump();
                Console.WriteLine($"Move to {screen.DeviceName}: WPF DPI {beforeDpi.DpiScaleX * 100:0}% -> {movedDpi.DpiScaleX * 100:0}%, native DPI={GetDpiForWindow(hwnd)}");
                if (beforeDpi.DpiScaleX != movedDpi.DpiScaleX)
                {
                    transitions++;
                    CheckWindowBounds(hwnd, desktop);
                    Console.WriteLine($"DPI transition {beforeDpi.DpiScaleX * 100:0}% -> {movedDpi.DpiScaleX * 100:0}%: physical desktop bounds restored");
                }
                else
                {
                    Invoke(overlay, "PositionOverlay");
                    Pump();
                }
            }
            Console.WriteLine($"Native DPI transitions observed: {transitions}");

            // Simulate only this test window's WM_DPICHANGED. This verifies WPF's
            // layout/crop path at 125%, 150% and 200% without changing OS settings.
            foreach (int dpi in new[] { 120, 144, 192, 96 })
            {
                SendDpiChanged(hwnd, desktop, dpi);
                Pump();
                Check(Math.Abs(VisualTreeHelper.GetDpi(overlay).DpiScaleX - dpi / 96.0) < 0.001,
                    $"Simulated DPI {dpi} reached WPF");
                CheckWindowBounds(hwnd, desktop);
                var selection = new Rect(desktop.Left + 123, desktop.Top + 456, 321, 234);
                SetField(overlay, "_startScreenPoint", selection.TopLeft);
                SetField(overlay, "_currentScreenPoint", selection.BottomRight);
                Invoke(overlay, "UpdateSelection");
                overlay.UpdateLayout();
                var border = (FrameworkElement)overlay.FindName("SelectionBorder");
                var topLeft = canvas.PointToScreen(new Point(Canvas.GetLeft(border), Canvas.GetTop(border)));
                var bottomRight = canvas.PointToScreen(new Point(Canvas.GetLeft(border) + border.Width, Canvas.GetTop(border) + border.Height));
                Check(Near(topLeft, selection.TopLeft) && Near(bottomRight, selection.BottomRight), $"Simulated {dpi} DPI selection border");
                CaptureEventArgs? completed = null;
                EventHandler<CaptureEventArgs> handler = (_, e) => completed = e;
                overlay.CaptureCompleted += handler;
                Invoke(overlay, "CompleteCapture", selection);
                overlay.CaptureCompleted -= handler;
                Check(completed?.CaptureRegion == selection && completed.CapturedImage?.PixelWidth == 321
                    && completed.CapturedImage.PixelHeight == 234, $"Simulated {dpi} DPI crop");
                Check((bool)Invoke(overlay, "ShowLastCaptureRegion")!
                    && (Rect)Invoke(overlay, "GetSelectionScreenRect")! == previous, $"Simulated {dpi} DPI previous region");
                Console.WriteLine($"Simulated {dpi / 96.0 * 100:0}%: physical bounds, selection border, crop and previous region verified");
            }
        }
        finally
        {
            overlay.Close();
            app.Shutdown();
        }
    }

    private static BitmapSource MakeSource(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 2] = (byte)(x / 256);
                pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, args);
    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
    private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        _checks++;
    }

    private static void CheckWindowBounds(IntPtr hwnd, Rect expected)
    {
        Check(GetWindowRect(hwnd, out var actual), "Read overlay HWND");
        Check(new Rect(actual.Left, actual.Top, actual.Right - actual.Left, actual.Bottom - actual.Top) == expected,
            $"Overlay HWND covers desktop: actual=({actual.Left},{actual.Top},{actual.Right},{actual.Bottom}) expected={expected}");
    }

    private static void SendDpiChanged(IntPtr hwnd, Rect desktop, int dpi)
    {
        var rect = new NativeRect { Left = (int)desktop.Left, Top = (int)desktop.Top,
            Right = (int)(desktop.Left + desktop.Width * dpi / 96.0),
            Bottom = (int)(desktop.Top + desktop.Height * dpi / 96.0) };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<NativeRect>());
        try
        {
            Marshal.StructureToPtr(rect, memory, false);
            SendMessage(hwnd, 0x02E0, new IntPtr(dpi | (dpi << 16)), memory);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
