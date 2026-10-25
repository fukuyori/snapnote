using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapNoteStudio.Services;
using WinForms = System.Windows.Forms;

namespace SnapNoteStudio.Views;

public partial class OverlayWindow : Window
{
    private const double DragThreshold = 5;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;

    private bool _isSelecting;
    private bool _isMouseDown;
    private bool _isShowingLastRegion;
    private bool _isCursorMessageVisible;
    private bool _isCountingDown;
    private int _captureDelaySeconds;
    private Point _startScreenPoint;
    private Point _currentScreenPoint;
    private Rect _captureScreenBounds;
    private BitmapSource? _screenCapture;
    private readonly Rect? _lastCaptureRegion;

    public event EventHandler<CaptureEventArgs>? CaptureCompleted;
    public event EventHandler? CaptureCancelled;

    public OverlayWindow(BitmapSource? screenCapture, Rect screenBounds, Rect? lastCaptureRegion = null)
    {
        InitializeComponent();
        _screenCapture = screenCapture;
        _captureScreenBounds = screenBounds;
        _lastCaptureRegion = lastCaptureRegion;
        Loaded += OnLoaded;
        SizeChanged += (_, _) => { if (IsLoaded) UpdateOverlayLayout(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PositionOverlay();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOverlay();
        UpdateLayout();
        UpdateOverlayLayout();
        LogCoordinates("loaded");

        // Activate and focus
        Activate();
        Focus();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // WPF may apply the WM_DPICHANGED suggested size. Restore the physical
        // desktop bounds after that handling, without treating them as DIPs.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!IsVisible || _isCountingDown) return;
            PositionOverlay();
            UpdateLayout();
            UpdateOverlayLayout();
            LogCoordinates("dpi-changed");
        }));
    }

    private void PositionOverlay()
    {
        const uint flags = 0x0004 | 0x0010; // SWP_NOZORDER | SWP_NOACTIVATE
        if (!SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
            (int)_captureScreenBounds.X, (int)_captureScreenBounds.Y,
            (int)_captureScreenBounds.Width, (int)_captureScreenBounds.Height, flags))
        {
            DiagnosticLog.Write($"CaptureCoordinates SetWindowPos failed error={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateOverlayLayout()
    {
        FullScreenGeometry.Rect = new Rect(0, 0, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
        CenterOnPrimaryScreen(Instructions);
        if (_isSelecting || _isShowingLastRegion) UpdateSelection();
    }

    private void CenterOnPrimaryScreen(FrameworkElement element)
    {
        var primary = WinForms.Screen.PrimaryScreen?.Bounds;
        if (primary == null) return;
        var center = OverlayCanvas.PointFromScreen(new Point(
            primary.Value.Left + primary.Value.Width / 2.0,
            primary.Value.Top + primary.Value.Height / 2.0));
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(element, center.X - element.DesiredSize.Width / 2);
        Canvas.SetTop(element, center.Y - element.DesiredSize.Height / 2);
    }

    private bool TryGetScreenPoint(out Point point)
    {
        if (GetPhysicalCursorPos(out var cursor))
        {
            point = new Point(cursor.X, cursor.Y);
            return true;
        }
        point = default;
        DiagnosticLog.Write($"CaptureCoordinates GetPhysicalCursorPos failed error={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
        CaptureCancelled?.Invoke(this, EventArgs.Empty);
        return false;
    }

    private void LogCoordinates(string stage)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var handle = new WindowInteropHelper(this).Handle;
        string window = GetWindowRect(handle, out var rect)
            ? $"({rect.Left},{rect.Top},{rect.Right - rect.Left},{rect.Bottom - rect.Top})" : "unavailable";
        DiagnosticLog.Write($"CaptureCoordinates stage={stage} screen={_captureScreenBounds} hwnd={window} "
            + $"overlayDip={OverlayCanvas.ActualWidth}x{OverlayCanvas.ActualHeight} dpi={dpi.DpiScaleX},{dpi.DpiScaleY} "
            + $"bitmap={_screenCapture?.PixelWidth}x{_screenCapture?.PixelHeight} "
            + $"startPx={_startScreenPoint} endPx={_currentScreenPoint}");
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isCountingDown)
        {
            if (e.Key == Key.Escape)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            return;
        }

        int timerSeconds = GetTimerSeconds(e.Key);
        if (timerSeconds > 0 && !_isMouseDown)
        {
            _captureDelaySeconds = timerSeconds;
            TimerInstruction.Text = $"{timerSeconds} 秒タイマーを設定（選択後に開始）";
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_isShowingLastRegion)
            {
                CancelLastRegionPreview();
            }
            else
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Space && !_isMouseDown)
        {
            if (_isShowingLastRegion)
            {
                CompleteCapture(GetSelectionScreenRect());
            }
            else
            {
                if (ShowLastCaptureRegion()) ShowCursorMessage("ウインドウ選択中");
            }
            e.Handled = true;
        }
        else if ((e.Key == Key.Enter || e.Key == Key.Return) && _isShowingLastRegion)
        {
            CompleteCapture(GetSelectionScreenRect());
            e.Handled = true;
        }
    }

    private void CancelLastRegionPreview()
    {
        _isShowingLastRegion = false;
        HideCursorMessage();
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeIndicator.Visibility = Visibility.Collapsed;
        SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
        Instructions.Visibility = Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCountingDown)
            return;

        if (!TryGetScreenPoint(out _startScreenPoint)) return;
        _currentScreenPoint = _startScreenPoint;
        LogCoordinates("mouse-down");
        _isMouseDown = true;
        _isSelecting = false;
        _isShowingLastRegion = false;
        HideCursorMessage();

        Instructions.Visibility = Visibility.Collapsed;

        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isCountingDown)
            return;

        if (_isCursorMessageVisible)
        {
            UpdateCursorMessagePosition(e.GetPosition(this));
        }

        if (!_isMouseDown)
            return;

        if (!TryGetScreenPoint(out _currentScreenPoint)) return;

        if (!_isSelecting && IsDragPastThreshold())
        {
            _isSelecting = true;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeIndicator.Visibility = Visibility.Visible;
        }

        if (!_isSelecting)
            return;

        UpdateSelection();
    }

    private void ShowCursorMessage(string message)
    {
        CursorMessageText.Text = message;
        CursorMessage.Visibility = Visibility.Visible;
        _isCursorMessageVisible = true;
        UpdateCursorMessagePosition(Mouse.GetPosition(this));
    }

    private void HideCursorMessage()
    {
        CursorMessage.Visibility = Visibility.Collapsed;
        _isCursorMessageVisible = false;
    }

    private void UpdateCursorMessagePosition(Point cursorPosition)
    {
        CursorMessage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double left = cursorPosition.X + 16;
        double top = cursorPosition.Y + 18;
        double width = CursorMessage.DesiredSize.Width;
        double height = CursorMessage.DesiredSize.Height;

        if (left + width > ActualWidth)
            left = cursorPosition.X - width - 16;

        if (top + height > ActualHeight)
            top = cursorPosition.Y - height - 18;

        Canvas.SetLeft(CursorMessage, Math.Max(0, left));
        Canvas.SetTop(CursorMessage, Math.Max(0, top));
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCountingDown)
            return;

        if (!_isMouseDown) return;

        if (!TryGetScreenPoint(out _currentScreenPoint)) return;
        LogCoordinates("mouse-up");
        _isMouseDown = false;
        ReleaseMouseCapture();

        if (_isSelecting)
        {
            _isSelecting = false;
            CompleteCapture(GetSelectionScreenRect());
        }
        else
        {
            CaptureWindowUnderCursor();
        }
    }

    private bool IsDragPastThreshold()
    {
        var start = OverlayCanvas.PointFromScreen(_startScreenPoint);
        var current = OverlayCanvas.PointFromScreen(_currentScreenPoint);
        return Math.Abs(current.X - start.X) >= DragThreshold
            || Math.Abs(current.Y - start.Y) >= DragThreshold;
    }

    private bool ShowLastCaptureRegion()
    {
        if (_lastCaptureRegion is not { Width: > 5, Height: > 5 } region)
            return false;

        region.Intersect(_captureScreenBounds);
        if (region.IsEmpty || region.Width <= 5 || region.Height <= 5) return false;
        _startScreenPoint = region.TopLeft;
        _currentScreenPoint = region.BottomRight;
        _isShowingLastRegion = true;

        Instructions.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;
        UpdateSelection();
        return true;
    }

    private void CaptureWindowUnderCursor()
    {
        var excludedHandle = new WindowInteropHelper(this).Handle;
        var windowRect = WindowSelectionService.GetWindowRectUnderCursor(excludedHandle);

        if (windowRect is null)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        // WindowSelectionService returns pixels relative to the current virtual desktop.
        var screenBounds = ScreenCaptureService.GetVirtualScreenBounds();
        var absoluteRect = windowRect.Value;
        absoluteRect.Offset(screenBounds.X, screenBounds.Y);
        CompleteCapture(absoluteRect);
    }

    private Rect ScreenRectToOverlayRect(Rect screenRect)
    {
        return new Rect(OverlayCanvas.PointFromScreen(screenRect.TopLeft),
            OverlayCanvas.PointFromScreen(screenRect.BottomRight));
    }

    private async void CompleteCapture(Rect selectionRect)
    {
        if (selectionRect.Width <= 5 || selectionRect.Height <= 5)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        var captureSource = _captureDelaySeconds > 0
            ? await CaptureScreenAfterCountdownAsync()
            : _screenCapture;

        if (captureSource == null)
            return;

        var crop = CaptureCoordinates.GetCropRect(selectionRect, _captureScreenBounds,
            captureSource.PixelWidth, captureSource.PixelHeight);
        DiagnosticLog.Write($"CaptureCoordinates selectedScreenPx={selectionRect} capturedScreenPx={_captureScreenBounds} cropPx={crop} "
            + $"bitmap={captureSource.PixelWidth}x{captureSource.PixelHeight} delay={_captureDelaySeconds}");
        if (crop.Width <= 5 || crop.Height <= 5)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        BitmapSource capturedImage;
        try
        {
            capturedImage = new CroppedBitmap(captureSource, crop);
            capturedImage.Freeze();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"CaptureCoordinates crop failed: {ex}");
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }
        CaptureCompleted?.Invoke(this, new CaptureEventArgs(capturedImage,
            CaptureCoordinates.ToScreenRect(crop, _captureScreenBounds)));
    }

    private async Task<BitmapSource?> CaptureScreenAfterCountdownAsync()
    {
        _isCountingDown = true;
        _isMouseDown = false;
        _isSelecting = false;
        ReleaseMouseCapture();

        DarkOverlay.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeIndicator.Visibility = Visibility.Collapsed;
        Instructions.Visibility = Visibility.Collapsed;
        HideCursorMessage();

        CountdownIndicator.Visibility = Visibility.Visible;
        CenterOnPrimaryScreen(CountdownIndicator);

        EnableMouseClickThrough();

        for (int remaining = _captureDelaySeconds; remaining > 0; remaining--)
        {
            CountdownText.Text = remaining.ToString();
            await Task.Delay(TimeSpan.FromSeconds(1));

            if (!IsLoaded)
                return null;
        }

        CountdownIndicator.Visibility = Visibility.Collapsed;
        Hide();

        // Let WPF and the desktop compositor remove the countdown before CopyFromScreen.
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(50);

        var captureSource = ScreenCaptureService.CaptureScreen(out var screenBounds);
        _captureScreenBounds = screenBounds;
        if (captureSource == null)
        {
            MessageBox.Show(
                L10n.Get("CaptureFailed"),
                L10n.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }

        return captureSource;
    }

    private void EnableMouseClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, extendedStyle | WsExTransparent);
    }

    private static int GetTimerSeconds(Key key)
    {
        return key switch
        {
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            Key.D6 or Key.NumPad6 => 6,
            Key.D7 or Key.NumPad7 => 7,
            Key.D8 or Key.NumPad8 => 8,
            Key.D9 or Key.NumPad9 => 9,
            _ => 0
        };
    }

    private void UpdateSelection()
    {
        var screenRect = GetSelectionScreenRect();
        var rect = ScreenRectToOverlayRect(screenRect);

        // Update selection geometry (transparent hole)
        SelectionGeometry.Rect = rect;

        // Update selection border
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        // Update size indicator - show physical pixel size
        SizeText.Text = $"{(int)screenRect.Width} × {(int)screenRect.Height}";
        
        // Position size indicator below selection
        double indicatorX = rect.X;
        double indicatorY = rect.Bottom + 8;
        
        // Keep indicator on screen
        if (indicatorY + 30 > Height)
            indicatorY = rect.Top - 35;
        
        Canvas.SetLeft(SizeIndicator, indicatorX);
        Canvas.SetTop(SizeIndicator, indicatorY);
    }

    private Rect GetSelectionScreenRect()
    {
        return new Rect(_startScreenPoint, _currentScreenPoint);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}
