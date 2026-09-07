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
    private Point _startPoint;
    private Point _currentPoint;
    private BitmapSource? _screenCapture;
    private readonly Rect? _lastCaptureRegion;

    public event EventHandler<CaptureEventArgs>? CaptureCompleted;
    public event EventHandler? CaptureCancelled;

    public OverlayWindow(BitmapSource? screenCapture, Rect? lastCaptureRegion = null)
    {
        InitializeComponent();
        _screenCapture = screenCapture;
        _lastCaptureRegion = lastCaptureRegion;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Use WPF SystemParameters for window positioning (already in DIPs)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        // Update full screen geometry
        FullScreenGeometry.Rect = new Rect(0, 0, Width, Height);

        // Position instructions in the center of the primary screen.
        Instructions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(
            Instructions,
            -SystemParameters.VirtualScreenLeft
                + (SystemParameters.PrimaryScreenWidth - Instructions.DesiredSize.Width) / 2);
        Canvas.SetTop(
            Instructions,
            -SystemParameters.VirtualScreenTop
                + (SystemParameters.PrimaryScreenHeight - Instructions.DesiredSize.Height) / 2);

        // Activate and focus
        Activate();
        Focus();
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
                CompleteCapture(GetSelectionRect());
            }
            else
            {
                ShowLastCaptureRegion();
                ShowCursorMessage("ウインドウ選択中");
            }
            e.Handled = true;
        }
        else if ((e.Key == Key.Enter || e.Key == Key.Return) && _isShowingLastRegion)
        {
            CompleteCapture(GetSelectionRect());
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

        _startPoint = e.GetPosition(this);
        _currentPoint = _startPoint;
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

        _currentPoint = e.GetPosition(this);

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

        _currentPoint = e.GetPosition(this);
        _isMouseDown = false;
        ReleaseMouseCapture();

        if (_isSelecting)
        {
            _isSelecting = false;
            CompleteCapture(GetSelectionRect());
        }
        else
        {
            CaptureWindowUnderCursor();
        }
    }

    private bool IsDragPastThreshold()
    {
        return Math.Abs(_currentPoint.X - _startPoint.X) >= DragThreshold
            || Math.Abs(_currentPoint.Y - _startPoint.Y) >= DragThreshold;
    }

    private void ShowLastCaptureRegion()
    {
        if (_lastCaptureRegion is not { Width: > 5, Height: > 5 } region)
            return;

        _startPoint = new Point(region.Left, region.Top);
        _currentPoint = new Point(region.Right, region.Bottom);
        _isShowingLastRegion = true;

        Instructions.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;
        UpdateSelection();
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

        CompleteWindowCapture(windowRect.Value);
    }

    private Rect ConvertScreenPixelsToOverlayRect(Rect screenPixelRect)
    {
        if (_screenCapture == null || ActualWidth <= 0 || ActualHeight <= 0)
            return screenPixelRect;

        double scaleX = _screenCapture.PixelWidth / ActualWidth;
        double scaleY = _screenCapture.PixelHeight / ActualHeight;

        return new Rect(
            screenPixelRect.X / scaleX,
            screenPixelRect.Y / scaleY,
            screenPixelRect.Width / scaleX,
            screenPixelRect.Height / scaleY);
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

        var capturedImage = CropBitmap(captureSource, selectionRect);
        CaptureCompleted?.Invoke(this, new CaptureEventArgs(capturedImage, selectionRect));
    }

    private async void CompleteWindowCapture(Rect screenPixelRect)
    {
        var captureSource = _captureDelaySeconds > 0
            ? await CaptureScreenAfterCountdownAsync()
            : _screenCapture;

        if (captureSource == null)
            return;

        var capturedImage = CropBitmapPixels(captureSource, screenPixelRect);
        var captureRegion = ConvertScreenPixelsToOverlayRect(screenPixelRect);
        CaptureCompleted?.Invoke(this, new CaptureEventArgs(capturedImage, captureRegion));
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
        CountdownIndicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(
            CountdownIndicator,
            -SystemParameters.VirtualScreenLeft
                + (SystemParameters.PrimaryScreenWidth - CountdownIndicator.DesiredSize.Width) / 2);
        Canvas.SetTop(
            CountdownIndicator,
            -SystemParameters.VirtualScreenTop
                + (SystemParameters.PrimaryScreenHeight - CountdownIndicator.DesiredSize.Height) / 2);

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

        var captureSource = ScreenCaptureService.CaptureScreen();
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
        var rect = GetSelectionRect();

        // Update selection geometry (transparent hole)
        SelectionGeometry.Rect = rect;

        // Update selection border
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        // Update size indicator - show physical pixel size
        if (_screenCapture != null && ActualWidth > 0 && ActualHeight > 0)
        {
            double scaleX = (double)_screenCapture.PixelWidth / ActualWidth;
            double scaleY = (double)_screenCapture.PixelHeight / ActualHeight;
            int pixelWidth = (int)Math.Round(rect.Width * scaleX);
            int pixelHeight = (int)Math.Round(rect.Height * scaleY);
            SizeText.Text = $"{pixelWidth} × {pixelHeight}";
        }
        else
        {
            SizeText.Text = $"{(int)rect.Width} × {(int)rect.Height}";
        }
        
        // Position size indicator below selection
        double indicatorX = rect.X;
        double indicatorY = rect.Bottom + 8;
        
        // Keep indicator on screen
        if (indicatorY + 30 > Height)
            indicatorY = rect.Top - 35;
        
        Canvas.SetLeft(SizeIndicator, indicatorX);
        Canvas.SetTop(SizeIndicator, indicatorY);
    }

    private Rect GetSelectionRect()
    {
        double x = Math.Min(_startPoint.X, _currentPoint.X);
        double y = Math.Min(_startPoint.Y, _currentPoint.Y);
        double width = Math.Abs(_currentPoint.X - _startPoint.X);
        double height = Math.Abs(_currentPoint.Y - _startPoint.Y);

        return new Rect(x, y, width, height);
    }

    private BitmapSource? CropBitmap(BitmapSource? source, Rect rect)
    {
        if (source == null) return null;

        double scaleX = (double)source.PixelWidth / ActualWidth;
        double scaleY = (double)source.PixelHeight / ActualHeight;

        int x = (int)Math.Round(rect.X * scaleX);
        int y = (int)Math.Round(rect.Y * scaleY);
        int width = (int)Math.Round(rect.Width * scaleX);
        int height = (int)Math.Round(rect.Height * scaleY);

        // Boundary check
        x = Math.Clamp(x, 0, source.PixelWidth - 1);
        y = Math.Clamp(y, 0, source.PixelHeight - 1);
        width = Math.Min(width, source.PixelWidth - x);
        height = Math.Min(height, source.PixelHeight - y);

        if (width <= 5 || height <= 5) return null;

        try
        {
            var croppedBitmap = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            croppedBitmap.Freeze();
            return croppedBitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private BitmapSource? CropBitmapPixels(BitmapSource? source, Rect pixelRect)
    {
        if (source == null) return null;

        int x = (int)Math.Round(pixelRect.X);
        int y = (int)Math.Round(pixelRect.Y);
        int width = (int)Math.Round(pixelRect.Width);
        int height = (int)Math.Round(pixelRect.Height);

        x = Math.Clamp(x, 0, source.PixelWidth - 1);
        y = Math.Clamp(y, 0, source.PixelHeight - 1);
        width = Math.Min(width, source.PixelWidth - x);
        height = Math.Min(height, source.PixelHeight - y);

        if (width <= 5 || height <= 5) return null;

        try
        {
            var croppedBitmap = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            croppedBitmap.Freeze();
            return croppedBitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}
