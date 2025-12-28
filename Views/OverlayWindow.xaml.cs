using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapNoteStudio.Services;
using WinForms = System.Windows.Forms;

namespace SnapNoteStudio.Views;

public partial class OverlayWindow : Window
{
    private bool _isSelecting;
    private Point _startPoint;
    private Point _currentPoint;
    private BitmapSource? _screenCapture;

    public event EventHandler<CaptureEventArgs>? CaptureCompleted;
    public event EventHandler? CaptureCancelled;

    public OverlayWindow(BitmapSource? screenCapture)
    {
        InitializeComponent();
        _screenCapture = screenCapture;
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

        // Position instructions in center of primary screen
        Canvas.SetLeft(Instructions, (SystemParameters.PrimaryScreenWidth - 200) / 2);
        Canvas.SetTop(Instructions, (SystemParameters.PrimaryScreenHeight - 100) / 2);

        // Activate and focus
        Activate();
        Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        _currentPoint = _startPoint;
        _isSelecting = true;

        Instructions.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;

        CaptureMouse();
        UpdateSelection();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        _currentPoint = e.GetPosition(this);
        UpdateSelection();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;

        _isSelecting = false;
        ReleaseMouseCapture();

        var selectionRect = GetSelectionRect();
        
        if (selectionRect.Width > 5 && selectionRect.Height > 5)
        {
            // Capture the selected region from the pre-captured screen
            var capturedImage = CropBitmap(_screenCapture, selectionRect);
            
            CaptureCompleted?.Invoke(this, new CaptureEventArgs(capturedImage, selectionRect));
        }
        else
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }
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
}
