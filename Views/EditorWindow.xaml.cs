using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SnapNoteStudio.Models;
using SnapNoteStudio.Services;

namespace SnapNoteStudio.Views;

public partial class EditorWindow : Window
{
    private const double MinZoomScale = 0.25;
    private const double MaxZoomScale = 12.0;
    private const double ZoomStep = 1.1;
    private const double EditorMargin = 20.0;

    private BitmapSource _capturedImage;
    private readonly PixelationRenderer _pixelationRenderer = new();
    private Rect _captureRegion;
    private readonly List<Annotation> _annotations = new();
    private readonly UndoRedoService _undoRedo = new();
    private readonly SettingsService? _settingsService;
    private double _zoomScale = 1.0;

    // Current tool state
    private enum Tool { Pan, Select, Arrow, Line, Rectangle, Ellipse, Text, Step, Highlighter,
                        FilledRect, Mosaic, Blur, Spotlight, Magnifier }
    private Tool _currentTool = Tool.Pan;
    private Color _currentColor = Colors.Red;
    private double _currentStrokeWidth = 3;
    private double _currentOpacity = 1.0;
    private int _nextStepNumber = 1;

    // Drawing state
    private bool _isDrawing;
    private Point _startPoint;
    private Annotation? _currentAnnotation;
    private Annotation? _selectedAnnotation;

    // For dragging
    private bool _isDragging;
    private Point _dragStartPoint;

    // For panning the editor viewport
    private bool _isPanning;
    private MouseButton _panButton;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    // Crop mode
    private bool _isCropMode;
    private Rectangle? _cropRect;
    private Point _cropStart;

    public EditorWindow(BitmapSource capturedImage, Rect captureRegion, SettingsService? settingsService = null)
    {
        InitializeComponent();
        _capturedImage = capturedImage;
        _captureRegion = captureRegion;
        _settingsService = settingsService;
        
        // Apply default settings
        if (_settingsService != null)
        {
            _currentStrokeWidth = _settingsService.Settings.DefaultStrokeWidth;
            _currentOpacity = _settingsService.Settings.DefaultOpacity;
        }
        
        Loaded += OnLoaded;
        Closing += OnClosing;
        _undoRedo.StateChanged += (s, e) => UpdateUndoRedoButtons();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BackgroundImage.Source = _capturedImage;

        // Apply settings to UI
        StrokeWidthSlider.Value = _currentStrokeWidth;
        OpacitySlider.Value = _currentOpacity;

        // Apply localization
        ApplyLocalization();

        UpdateStatus();

        RestoreWindowBounds();

        UpdateUndoRedoButtons();

        AutoOcrToggle.IsChecked = _settingsService?.Settings.AutoOcrOnCapture ?? false;
        AutoTranslateToggle.IsChecked = _settingsService?.Settings.AutoTranslateOnCapture ?? false;

        RunAutoOcrPipelineIfEnabled();
    }

    private void AutoOcrToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settingsService == null)
            return;

        _settingsService.Settings.AutoOcrOnCapture = AutoOcrToggle.IsChecked == true;
        _settingsService.Save();
    }

    private void AutoTranslateToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settingsService == null)
            return;

        _settingsService.Settings.AutoTranslateOnCapture = AutoTranslateToggle.IsChecked == true;
        _settingsService.Save();
    }

    private async void RunAutoOcrPipelineIfEnabled()
    {
        if (AutoTranslateToggle.IsChecked == true)
            await RunOcrPipelineAsync(autoTranslate: true);
        else if (AutoOcrToggle.IsChecked == true)
            await RunOcrPipelineAsync(autoTranslate: false);
    }

    private void RestoreWindowBounds()
    {
        var bounds = _settingsService?.Settings.EditorWindowBounds;

        if (bounds is { Width: > 0, Height: > 0 } && IsOnScreen(bounds))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            WindowState = bounds.IsMaximized ? WindowState.Maximized : WindowState.Normal;
            return;
        }

        double maxWidth = SystemParameters.WorkArea.Width * 0.9;
        double maxHeight = SystemParameters.WorkArea.Height * 0.9;

        // Ensure minimum size of 1200x800, or larger if image requires
        double desiredWidth = Math.Max(1200, _capturedImage.PixelWidth + 200);
        double desiredHeight = Math.Max(800, _capturedImage.PixelHeight + 180);

        Width = Math.Min(desiredWidth, maxWidth);
        Height = Math.Min(desiredHeight, maxHeight);
    }

    private static bool IsOnScreen(SavedWindowBounds bounds)
    {
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var windowRect = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        windowRect.Intersect(virtualScreen);

        // Require a reasonable portion of the saved window to still be visible
        return windowRect.Width >= 100 && windowRect.Height >= 100;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settingsService == null)
            return;

        var isMaximized = WindowState == WindowState.Maximized;
        var normalBounds = isMaximized ? RestoreBounds : new Rect(Left, Top, Width, Height);

        _settingsService.Settings.EditorWindowBounds = new SavedWindowBounds
        {
            Left = normalBounds.Left,
            Top = normalBounds.Top,
            Width = normalBounds.Width,
            Height = normalBounds.Height,
            IsMaximized = isMaximized
        };

        _settingsService.Save();
    }

    private void ApplyLocalization()
    {
        Title = $"{L10n.Get("EditorTitle")} {AppVersionInfo.Current}";
        
        // Header buttons
        ReuseWindowToggle.Content = L10n.Get("NewCapture");
        ReuseWindowToggle.ToolTip = L10n.Get("NewCaptureTooltip");
        AutoOcrToggle.Content = L10n.Get("AutoOcrToggle");
        AutoOcrToggle.ToolTip = L10n.Get("AutoOcrToggleTooltip");
        AutoTranslateToggle.Content = L10n.Get("AutoTranslateToggle");
        AutoTranslateToggle.ToolTip = L10n.Get("AutoTranslateToggleTooltip");
        UndoButton.Content = L10n.Get("Undo");
        RedoButton.Content = L10n.Get("Redo");
        CopyButton.Content = L10n.Get("Copy");
        SaveButton.Content = L10n.Get("Save");
        OpenSaveDirectoryMenuItem.Header = L10n.Get("OpenSaveDirectory");
        
        // Header labels
        ColorLabel.Text = L10n.Get("Color");
        ThicknessLabel.Text = L10n.Get("Thickness");
        OpacityLabel.Text = L10n.Get("Opacity");
        
        // Sidebar section labels
        DrawingLabel.Text = L10n.Get("Drawing");
        EffectsLabel.Text = L10n.Get("Effects");
        ImageLabel.Text = L10n.Get("Image");
        
        // Tool tooltips
        PanToolButton.ToolTip = L10n.Get("ToolPan");
        SelectToolButton.ToolTip = L10n.Get("ToolSelect");
        ArrowToolButton.ToolTip = L10n.Get("ToolArrow");
        LineToolButton.ToolTip = L10n.Get("ToolLine");
        RectToolButton.ToolTip = L10n.Get("ToolRect");
        EllipseToolButton.ToolTip = L10n.Get("ToolEllipse");
        TextToolButton.ToolTip = L10n.Get("ToolText");
        StepToolButton.ToolTip = L10n.Get("ToolStep");
        HighlighterToolButton.ToolTip = L10n.Get("ToolHighlighter");
        FilledRectToolButton.ToolTip = L10n.Get("ToolFilled");
        MosaicToolButton.ToolTip = L10n.Get("ToolMosaic");
        BlurToolButton.ToolTip = L10n.Get("ToolBlur");
        SpotlightToolButton.ToolTip = L10n.Get("ToolSpotlight");
        MagnifierToolButton.ToolTip = L10n.Get("ToolMagnifier");
        
        // Image operation buttons
        CropButton.Content = L10n.Get("ToolCrop");
        RotateButton.Content = L10n.Get("ToolRotate");
        ResizeButton.Content = L10n.Get("ToolResize");
        SharpenButton.Content = L10n.Get("ToolSharpen");
        OcrButton.Content = L10n.Get("ToolOcr");
        OcrButton.ToolTip = L10n.Get("OcrTooltip");
        OcrTranslateQuickButton.Content = L10n.Get("ToolOcrTranslate");
        OcrTranslateQuickButton.ToolTip = L10n.Get("OcrTranslateQuickTooltip");

        // OCR pane
        OcrPaneTitle.Text = L10n.Get("OcrResultTitle");
        OcrPaneCloseButton.ToolTip = L10n.Get("Close");
        OcrTranslateLabel.Text = L10n.Get("TranslateTo");
        OcrTranslateButton.Content = L10n.Get("Translate");
        OcrCopyButton.Content = L10n.Get("Copy");
    }

    private void UpdateStatus()
    {
        var sizeText = string.Format(L10n.Get("Size"), _capturedImage.PixelWidth, _capturedImage.PixelHeight);
        var zoomText = string.Format(L10n.Get("Zoom"), _zoomScale * 100);
        StatusText.Text = $"{sizeText} | {zoomText}";
        StepCountText.Text = _nextStepNumber > 1 ? string.Format(L10n.Get("NextStep"), _nextStepNumber) : "";
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _undoRedo.CanUndo;
        RedoButton.IsEnabled = _undoRedo.CanRedo;
    }

    #region Tool Selection

    private void PanTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Pan;
    private void SelectTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Select;
    private void ArrowTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Arrow;
    private void LineTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Line;
    private void RectTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Rectangle;
    private void EllipseTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Ellipse;
    private void TextTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Text;
    private void StepTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Step;
    private void HighlighterTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Highlighter;
    private void FilledRectTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.FilledRect;
    private void MosaicTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Mosaic;
    private void BlurTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Blur;
    private void SpotlightTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Spotlight;
    private void MagnifierTool_Checked(object sender, RoutedEventArgs e) => _currentTool = Tool.Magnifier;

    private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Not used anymore - replaced by ColorButton_Checked
    }

    private void ColorButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string colorStr)
        {
            _currentColor = (Color)ColorConverter.ConvertFromString(colorStr);
            if (_selectedAnnotation != null)
            {
                _selectedAnnotation.StrokeColor = _currentColor;
                RedrawAnnotations();
            }
        }
    }

    private void StrokeWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Not used anymore - replaced by slider
    }

    private void StrokeWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentStrokeWidth = e.NewValue;
        if (StrokeWidthText != null)
            StrokeWidthText.Text = ((int)e.NewValue).ToString();
        if (_selectedAnnotation != null)
        {
            _selectedAnnotation.StrokeThickness = _currentStrokeWidth;
            RedrawAnnotations();
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentOpacity = e.NewValue;
        if (OpacityText != null)
            OpacityText.Text = $"{(int)(e.NewValue * 100)}%";
    }

    #endregion

    #region Canvas Mouse Events

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(EditorCanvas);
        
        if (_isCropMode)
        {
            StartCropSelection(_startPoint);
            EditorCanvas.CaptureMouse();
        }
        else if (_currentTool == Tool.Select)
        {
            HandleSelectMouseDown(_startPoint);
            EditorCanvas.CaptureMouse();
        }
        else if (_currentTool == Tool.Text)
        {
            HandleTextToolClick(_startPoint);
            // Don't capture mouse - dialog is shown
        }
        else if (_currentTool == Tool.Step)
        {
            HandleStepToolClick(_startPoint);
            // Don't capture mouse - click-based tool
        }
        else
        {
            StartDrawing(_startPoint);
            EditorCanvas.CaptureMouse();
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var currentPoint = e.GetPosition(EditorCanvas);
        
        if (_isCropMode && _cropRect != null)
        {
            UpdateCropSelection(currentPoint);
        }
        else if (_isDragging && _selectedAnnotation != null)
        {
            var delta = currentPoint - _dragStartPoint;
            _selectedAnnotation.Move(delta);
            _dragStartPoint = currentPoint;
            RedrawAnnotations();
        }
        else if (_isDrawing && _currentAnnotation != null)
        {
            UpdateDrawing(currentPoint);
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EditorCanvas.ReleaseMouseCapture();
        
        if (_isCropMode && _cropRect != null)
        {
            FinishCropSelection();
        }
        else if (_isDragging)
        {
            _isDragging = false;
        }
        else if (_isDrawing && _currentAnnotation != null)
        {
            FinishDrawing();
        }
    }

    private void EditorScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_isDrawing || _isDragging || _isPanning)
            return;

        var rawZoomPoint = e.GetPosition(EditorCanvas);
        var zoomPoint = new Point(
            Math.Clamp(rawZoomPoint.X, 0, EditorCanvas.ActualWidth),
            Math.Clamp(rawZoomPoint.Y, 0, EditorCanvas.ActualHeight));
        var viewportPoint = e.GetPosition(EditorScrollViewer);
        var oldZoom = _zoomScale;
        var zoomFactor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var newZoom = Math.Clamp(_zoomScale * zoomFactor, MinZoomScale, MaxZoomScale);

        if (Math.Abs(newZoom - oldZoom) < 0.001)
        {
            e.Handled = true;
            return;
        }

        _zoomScale = newZoom;
        EditorZoomTransform.ScaleX = _zoomScale;
        EditorZoomTransform.ScaleY = _zoomScale;
        EditorContainer.UpdateLayout();

        EditorScrollViewer.ScrollToHorizontalOffset(
            Math.Max(0, EditorMargin + zoomPoint.X * _zoomScale - viewportPoint.X));
        EditorScrollViewer.ScrollToVerticalOffset(
            Math.Max(0, EditorMargin + zoomPoint.Y * _zoomScale - viewportPoint.Y));

        UpdateStatus();
        e.Handled = true;
    }

    private void EditorScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanStartPan(e.ChangedButton))
            return;

        if (_isDrawing || _isDragging)
            return;

        StartPan(e.ChangedButton, e.GetPosition(EditorScrollViewer));
        e.Handled = true;
    }

    private void EditorScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
            return;

        var currentPoint = e.GetPosition(EditorScrollViewer);
        var delta = currentPoint - _panStartPoint;

        EditorScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
        EditorScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
        e.Handled = true;
    }

    private void EditorScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning || e.ChangedButton != _panButton)
            return;

        EndPan();
        e.Handled = true;
    }

    private bool CanStartPan(MouseButton button)
    {
        return button is MouseButton.Right or MouseButton.Middle
            || (button == MouseButton.Left && _currentTool == Tool.Pan && !_isCropMode);
    }

    private void StartPan(MouseButton button, Point startPoint)
    {
        _isPanning = true;
        _panButton = button;
        _panStartPoint = startPoint;
        _panStartHorizontalOffset = EditorScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = EditorScrollViewer.VerticalOffset;

        Mouse.Capture(EditorScrollViewer);
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void EditorScrollViewer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPanning && Mouse.Captured != EditorScrollViewer)
            EndPan();
    }

    private void EndPan()
    {
        _isPanning = false;
        if (Mouse.Captured == EditorScrollViewer)
            Mouse.Capture(null);
        Mouse.OverrideCursor = null;
    }

    private void HandleSelectMouseDown(Point point)
    {
        var clickedAnnotation = FindAnnotationAt(point);
        
        if (clickedAnnotation != null)
        {
            SelectAnnotation(clickedAnnotation);
            _isDragging = true;
            _dragStartPoint = point;
        }
        else
        {
            SelectAnnotation(null);
        }
    }

    private void HandleTextToolClick(Point point)
    {
        var dialog = new TextInputDialog { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var textAnnotation = new TextAnnotation
            {
                Position = point,
                Text = dialog.InputText,
                StrokeColor = _currentColor,
                FontSize = dialog.SelectedFontSize
            };
            
            _undoRedo.Execute(new AddAnnotationAction(_annotations, textAnnotation));
            RedrawAnnotations();
        }
    }

    private void HandleStepToolClick(Point point)
    {
        var stepAnnotation = new StepAnnotation
        {
            Position = point,
            StepNumber = _nextStepNumber,
            StrokeColor = _currentColor,
            Size = 32 + _currentStrokeWidth * 2
        };
        
        _undoRedo.Execute(new AddAnnotationAction(_annotations, stepAnnotation));
        _nextStepNumber++;
        UpdateStatus();
        RedrawAnnotations();
    }

    private void StartDrawing(Point point)
    {
        _isDrawing = true;
        
        _currentAnnotation = _currentTool switch
        {
            Tool.Arrow => new ArrowAnnotation 
            { 
                StartPoint = point, EndPoint = point,
                StrokeColor = _currentColor, StrokeThickness = _currentStrokeWidth,
                Opacity = _currentOpacity
            },
            Tool.Line => new LineAnnotation 
            { 
                StartPoint = point, EndPoint = point,
                StrokeColor = _currentColor, StrokeThickness = _currentStrokeWidth,
                Opacity = _currentOpacity
            },
            Tool.Rectangle => new RectangleAnnotation 
            { 
                Bounds = new Rect(point, point),
                StrokeColor = _currentColor, StrokeThickness = _currentStrokeWidth,
                Opacity = _currentOpacity
            },
            Tool.Ellipse => new EllipseAnnotation 
            { 
                Bounds = new Rect(point, point),
                StrokeColor = _currentColor, StrokeThickness = _currentStrokeWidth,
                Opacity = _currentOpacity
            },
            Tool.Highlighter => new HighlighterAnnotation 
            { 
                Points = new List<Point> { point },
                HighlightWidth = 20 + _currentStrokeWidth * 2,
                HighlightColor = Color.FromArgb((byte)(128 * _currentOpacity), _currentColor.R, _currentColor.G, _currentColor.B),
                Opacity = _currentOpacity
            },
            Tool.FilledRect => new FilledRectAnnotation 
            { 
                Bounds = new Rect(point, point),
                FillColor = Color.FromArgb((byte)(255 * _currentOpacity), _currentColor.R, _currentColor.G, _currentColor.B),
                Opacity = _currentOpacity
            },
            Tool.Mosaic => new MosaicAnnotation 
            { 
                Bounds = new Rect(point, point),
                BlockSize = (int)(3 + (1 - _currentOpacity) * 20 + _currentStrokeWidth), // 濃さでブロックサイズ調整
                Opacity = _currentOpacity
            },
            Tool.Blur => new BlurAnnotation 
            { 
                Bounds = new Rect(point, point),
                BlurRadius = 5 + (1 - _currentOpacity) * 25 + _currentStrokeWidth * 2, // 濃さでぼかし強度調整
                Opacity = _currentOpacity
            },
            Tool.Spotlight => new SpotlightAnnotation 
            { 
                Bounds = new Rect(point, point),
                DarknessOpacity = 0.3 + _currentOpacity * 0.5, // 濃さで暗さ調整
                Opacity = _currentOpacity
            },
            Tool.Magnifier => new MagnifierAnnotation
            {
                SourceCenter = point,
                DisplayPosition = point,
                SourceRadius = 1,
                ZoomFactor = 1.5 + _currentOpacity * 1.5, // 濃さで拡大率調整 (1.5x - 3x)
                StrokeColor = _currentColor,
                Opacity = _currentOpacity
            },
            _ => null
        };
        
        if (_currentAnnotation != null)
        {
            _annotations.Add(_currentAnnotation);
            RedrawAnnotations();
        }
    }

    private void UpdateDrawing(Point point)
    {
        switch (_currentAnnotation)
        {
            case ArrowAnnotation arrow:
                arrow.EndPoint = point;
                break;
            case LineAnnotation line:
                line.EndPoint = point;
                break;
            case RectangleAnnotation rect:
                rect.Bounds = new Rect(_startPoint, point);
                break;
            case EllipseAnnotation ellipse:
                ellipse.Bounds = new Rect(_startPoint, point);
                break;
            case HighlighterAnnotation highlighter:
                highlighter.Points.Add(point);
                break;
            case FilledRectAnnotation filled:
                filled.Bounds = new Rect(_startPoint, point);
                break;
            case MosaicAnnotation mosaic:
                mosaic.Bounds = new Rect(_startPoint, point);
                break;
            case BlurAnnotation blur:
                blur.Bounds = new Rect(_startPoint, point);
                break;
            case SpotlightAnnotation spotlight:
                spotlight.Bounds = new Rect(_startPoint, point);
                break;
            case MagnifierAnnotation mag:
                // ドラッグ範囲から拡大鏡のサイズを計算
                var dragRect = new Rect(_startPoint, point);
                mag.SourceCenter = new Point(dragRect.X + dragRect.Width / 2, dragRect.Y + dragRect.Height / 2);
                mag.SourceRadius = Math.Max(dragRect.Width, dragRect.Height) / 2;
                // 表示位置は右下にオフセット
                mag.DisplayPosition = new Point(dragRect.Right + 20, dragRect.Bottom + 20);
                break;
        }
        
        RedrawAnnotations();
    }

    private void FinishDrawing()
    {
        _isDrawing = false;
        
        if (_currentAnnotation != null)
        {
            var bounds = _currentAnnotation.GetBounds();
            bool tooSmall = bounds.Width < 5 && bounds.Height < 5;
            
            if (_currentAnnotation is HighlighterAnnotation hl)
                tooSmall = hl.Points.Count < 2;
            
            if (tooSmall)
            {
                _annotations.Remove(_currentAnnotation);
            }
            else
            {
                _annotations.Remove(_currentAnnotation);
                _undoRedo.Execute(new AddAnnotationAction(_annotations, _currentAnnotation));
            }
        }
        
        _currentAnnotation = null;
        RedrawAnnotations();
    }

    private Annotation? FindAnnotationAt(Point point)
    {
        for (int i = _annotations.Count - 1; i >= 0; i--)
        {
            var bounds = _annotations[i].GetBounds();
            bounds.Inflate(5, 5);
            if (bounds.Contains(point))
                return _annotations[i];
        }
        return null;
    }

    private void SelectAnnotation(Annotation? annotation)
    {
        if (_selectedAnnotation != null)
            _selectedAnnotation.IsSelected = false;
        
        _selectedAnnotation = annotation;
        
        if (_selectedAnnotation != null)
            _selectedAnnotation.IsSelected = true;
        
        RedrawAnnotations();
    }

    #endregion

    #region Image Operations (Crop, Rotate, Resize)

    private void CropButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCropMode)
        {
            CancelCropMode();
        }
        else
        {
            _isCropMode = true;
            StatusText.Text = L10n.Get("CropInstruction");
            EditorCanvas.Cursor = System.Windows.Input.Cursors.Cross;
        }
    }

    private void StartCropSelection(Point point)
    {
        _cropStart = point;
        _cropRect = new Rectangle
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255))
        };
        EditorCanvas.Children.Add(_cropRect);
    }

    private void UpdateCropSelection(Point point)
    {
        if (_cropRect == null) return;
        
        var rect = new Rect(_cropStart, point);
        Canvas.SetLeft(_cropRect, rect.X);
        Canvas.SetTop(_cropRect, rect.Y);
        _cropRect.Width = rect.Width;
        _cropRect.Height = rect.Height;
    }

    private void FinishCropSelection()
    {
        if (_cropRect == null) return;
        
        var rect = new Rect(
            Canvas.GetLeft(_cropRect),
            Canvas.GetTop(_cropRect),
            _cropRect.Width,
            _cropRect.Height);
        
        EditorCanvas.Children.Remove(_cropRect);
        _cropRect = null;
        
        if (rect.Width > 10 && rect.Height > 10)
        {
            var result = MessageBox.Show(L10n.Get("CropConfirm"), L10n.Get("CropTitle"), 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                ApplyCrop(rect);
            }
        }
        
        CancelCropMode();
    }

    private void ApplyCrop(Rect rect)
    {
        int x = Math.Max(0, (int)rect.X);
        int y = Math.Max(0, (int)rect.Y);
        int width = Math.Min((int)rect.Width, _capturedImage.PixelWidth - x);
        int height = Math.Min((int)rect.Height, _capturedImage.PixelHeight - y);
        
        if (width <= 0 || height <= 0) return;
        
        var croppedBitmap = new CroppedBitmap(_capturedImage, new Int32Rect(x, y, width, height));
        _capturedImage = ConvertToWriteableBitmap(croppedBitmap);
        _pixelationRenderer.Clear();
        BackgroundImage.Source = _capturedImage;
        
        // Adjust annotations
        foreach (var annotation in _annotations.ToList())
        {
            annotation.Move(new Vector(-x, -y));
            if (!new Rect(0, 0, width, height).IntersectsWith(annotation.GetBounds()))
            {
                _annotations.Remove(annotation);
            }
        }
        
        RedrawAnnotations();
        UpdateStatus();
    }

    private void CancelCropMode()
    {
        _isCropMode = false;
        if (_cropRect != null)
        {
            EditorCanvas.Children.Remove(_cropRect);
            _cropRect = null;
        }
        EditorCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
        UpdateStatus();
    }

    private void RotateButton_Click(object sender, RoutedEventArgs e)
    {
        var rotated = new TransformedBitmap(_capturedImage, new RotateTransform(90));
        _capturedImage = ConvertToWriteableBitmap(rotated);
        _pixelationRenderer.Clear();
        BackgroundImage.Source = _capturedImage;
        
        // Rotate annotations
        double oldWidth = _capturedImage.PixelHeight; // After rotation, old height becomes width
        foreach (var annotation in _annotations)
        {
            RotateAnnotation90(annotation, oldWidth);
        }
        
        RedrawAnnotations();
        UpdateStatus();
    }

    private void RotateAnnotation90(Annotation annotation, double newWidth)
    {
        switch (annotation)
        {
            case ArrowAnnotation arrow:
                arrow.StartPoint = RotatePoint90(arrow.StartPoint, newWidth);
                arrow.EndPoint = RotatePoint90(arrow.EndPoint, newWidth);
                break;
            case LineAnnotation line:
                line.StartPoint = RotatePoint90(line.StartPoint, newWidth);
                line.EndPoint = RotatePoint90(line.EndPoint, newWidth);
                break;
            case TextAnnotation text:
                text.Position = RotatePoint90(text.Position, newWidth);
                break;
            case StepAnnotation step:
                step.Position = RotatePoint90(step.Position, newWidth);
                break;
            default:
                var bounds = annotation.GetBounds();
                var newTopLeft = RotatePoint90(new Point(bounds.X, bounds.Y + bounds.Height), newWidth);
                var delta = newTopLeft - new Point(bounds.X, bounds.Y);
                annotation.Move(delta);
                break;
        }
    }

    private Point RotatePoint90(Point p, double newWidth)
    {
        return new Point(newWidth - p.Y, p.X);
    }

    private void ResizeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ResizeDialog(_capturedImage.PixelWidth, _capturedImage.PixelHeight) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            double scaleX = dialog.NewWidth / (double)_capturedImage.PixelWidth;
            double scaleY = dialog.NewHeight / (double)_capturedImage.PixelHeight;
            
            var scaled = new TransformedBitmap(_capturedImage, new ScaleTransform(scaleX, scaleY));
            _capturedImage = ConvertToWriteableBitmap(scaled);
            _pixelationRenderer.Clear();
            BackgroundImage.Source = _capturedImage;
            
            // Scale annotations
            foreach (var annotation in _annotations)
            {
                ScaleAnnotation(annotation, scaleX, scaleY);
            }
            
            RedrawAnnotations();
            UpdateStatus();
        }
    }

    private void SharpenButton_Click(object sender, RoutedEventArgs e)
    {
        var sharpenedImage = ApplySharpenFilter(_capturedImage);
        _undoRedo.Execute(new ReplaceImageAction(_capturedImage, sharpenedImage, SetCapturedImage, "Sharpen image"));
    }

    private void SetCapturedImage(BitmapSource image)
    {
        _capturedImage = image;
        _pixelationRenderer.Clear();
        BackgroundImage.Source = _capturedImage;
        RedrawAnnotations();
        UpdateStatus();
    }

    /// <summary>
    /// Raised when the user toggles "show new captures in this window". App listens for this
    /// to decide whether the next hotkey-triggered capture should reuse this window instead of
    /// opening a new EditorWindow.
    /// </summary>
    public event EventHandler<bool>? ReuseForNewCaptureChanged;

    private bool _suppressReuseEvent;

    public void SetReuseForNewCapture(bool enabled)
    {
        _suppressReuseEvent = true;
        ReuseWindowToggle.IsChecked = enabled;
        _suppressReuseEvent = false;
    }

    private void ReuseWindowToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!_suppressReuseEvent)
            ReuseForNewCaptureChanged?.Invoke(this, true);
    }

    private void ReuseWindowToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_suppressReuseEvent)
            ReuseForNewCaptureChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Loads a newly captured image into this window in place of the current one. Returns false
    /// (without loading) if the user declines to discard existing annotations, so the caller can
    /// fall back to opening a separate EditorWindow instead of losing the capture.
    /// </summary>
    public bool TryLoadCapture(BitmapSource image, Rect captureRegion)
    {
        if (_annotations.Count > 0)
        {
            var result = MessageBox.Show(L10n.Get("NewCaptureConfirm"), L10n.Get("NewCaptureTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return false;
        }

        HideOcrPane();

        _capturedImage = image;
        _captureRegion = captureRegion;
        _pixelationRenderer.Clear();
        _annotations.Clear();
        _selectedAnnotation = null;
        _nextStepNumber = 1;
        _undoRedo.Clear();

        _zoomScale = 1.0;
        EditorZoomTransform.ScaleX = 1.0;
        EditorZoomTransform.ScaleY = 1.0;

        BackgroundImage.Source = _capturedImage;
        RedrawAnnotations();
        UpdateStatus();

        RunAutoOcrPipelineIfEnabled();
        return true;
    }

    private BitmapSource ApplySharpenFilter(BitmapSource source)
    {
        const double sharpenAmount = 0.35;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        var sourcePixels = new byte[stride * height];
        var targetPixels = new byte[sourcePixels.Length];

        converted.CopyPixels(sourcePixels, stride, 0);
        Array.Copy(sourcePixels, targetPixels, sourcePixels.Length);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * stride + x * 4;
                int left = index - 4;
                int right = index + 4;
                int top = index - stride;
                int bottom = index + stride;

                for (int channel = 0; channel < 3; channel++)
                {
                    int edge = sourcePixels[index + channel] * 4
                        - sourcePixels[left + channel]
                        - sourcePixels[right + channel]
                        - sourcePixels[top + channel]
                        - sourcePixels[bottom + channel];
                    int sharpened = (int)Math.Round(sourcePixels[index + channel] + edge * sharpenAmount);

                    targetPixels[index + channel] = ClampToByte(sharpened);
                }

                targetPixels[index + 3] = sourcePixels[index + 3];
            }
        }

        var bitmap = new WriteableBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), targetPixels, stride, 0);
        return bitmap;
    }

    private static byte ClampToByte(int value)
    {
        return (byte)Math.Clamp(value, 0, 255);
    }

    private class ReplaceImageAction : IUndoableAction
    {
        private readonly BitmapSource _oldImage;
        private readonly BitmapSource _newImage;
        private readonly Action<BitmapSource> _applyImage;

        public string Description { get; }

        public ReplaceImageAction(BitmapSource oldImage, BitmapSource newImage, Action<BitmapSource> applyImage, string description)
        {
            _oldImage = oldImage;
            _newImage = newImage;
            _applyImage = applyImage;
            Description = description;
        }

        public void Execute()
        {
            _applyImage(_newImage);
        }

        public void Undo()
        {
            _applyImage(_oldImage);
        }
    }

    private void ScaleAnnotation(Annotation annotation, double scaleX, double scaleY)
    {
        switch (annotation)
        {
            case ArrowAnnotation arrow:
                arrow.StartPoint = new Point(arrow.StartPoint.X * scaleX, arrow.StartPoint.Y * scaleY);
                arrow.EndPoint = new Point(arrow.EndPoint.X * scaleX, arrow.EndPoint.Y * scaleY);
                break;
            case LineAnnotation line:
                line.StartPoint = new Point(line.StartPoint.X * scaleX, line.StartPoint.Y * scaleY);
                line.EndPoint = new Point(line.EndPoint.X * scaleX, line.EndPoint.Y * scaleY);
                break;
            case TextAnnotation text:
                text.Position = new Point(text.Position.X * scaleX, text.Position.Y * scaleY);
                text.FontSize *= (scaleX + scaleY) / 2;
                break;
            case StepAnnotation step:
                step.Position = new Point(step.Position.X * scaleX, step.Position.Y * scaleY);
                step.Size *= (scaleX + scaleY) / 2;
                break;
            case HighlighterAnnotation hl:
                for (int i = 0; i < hl.Points.Count; i++)
                    hl.Points[i] = new Point(hl.Points[i].X * scaleX, hl.Points[i].Y * scaleY);
                break;
            case RectangleAnnotation rect:
                rect.Bounds = new Rect(rect.Bounds.X * scaleX, rect.Bounds.Y * scaleY, 
                                       rect.Bounds.Width * scaleX, rect.Bounds.Height * scaleY);
                break;
            case EllipseAnnotation ellipse:
                ellipse.Bounds = new Rect(ellipse.Bounds.X * scaleX, ellipse.Bounds.Y * scaleY,
                                          ellipse.Bounds.Width * scaleX, ellipse.Bounds.Height * scaleY);
                break;
            case FilledRectAnnotation filled:
                filled.Bounds = new Rect(filled.Bounds.X * scaleX, filled.Bounds.Y * scaleY,
                                         filled.Bounds.Width * scaleX, filled.Bounds.Height * scaleY);
                break;
            case MosaicAnnotation mosaic:
                mosaic.Bounds = new Rect(mosaic.Bounds.X * scaleX, mosaic.Bounds.Y * scaleY,
                                         mosaic.Bounds.Width * scaleX, mosaic.Bounds.Height * scaleY);
                break;
            case BlurAnnotation blur:
                blur.Bounds = new Rect(blur.Bounds.X * scaleX, blur.Bounds.Y * scaleY,
                                       blur.Bounds.Width * scaleX, blur.Bounds.Height * scaleY);
                break;
            case SpotlightAnnotation spotlight:
                spotlight.Bounds = new Rect(spotlight.Bounds.X * scaleX, spotlight.Bounds.Y * scaleY,
                                            spotlight.Bounds.Width * scaleX, spotlight.Bounds.Height * scaleY);
                break;
            case MagnifierAnnotation mag:
                mag.SourceCenter = new Point(mag.SourceCenter.X * scaleX, mag.SourceCenter.Y * scaleY);
                mag.DisplayPosition = new Point(mag.DisplayPosition.X * scaleX, mag.DisplayPosition.Y * scaleY);
                mag.SourceRadius *= (scaleX + scaleY) / 2;
                break;
        }
    }

    private BitmapSource ConvertToWriteableBitmap(BitmapSource source)
    {
        var wb = new WriteableBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        converted.CopyPixels(pixels, stride, 0);
        wb.WritePixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), pixels, stride, 0);
        
        return wb;
    }

    #endregion

    #region Annotation Rendering

    private void RedrawAnnotations()
    {
        EditorCanvas.Children.Clear();
        
        // First pass: render spotlight darkening (needs to be behind other annotations)
        foreach (var annotation in _annotations)
        {
            if (annotation is SpotlightAnnotation spotlight)
            {
                RenderSpotlight(spotlight);
            }
        }
        
        // Second pass: render all other annotations
        foreach (var annotation in _annotations)
        {
            if (annotation is not SpotlightAnnotation)
            {
                var shape = CreateShape(annotation);
                if (shape != null)
                    EditorCanvas.Children.Add(shape);
            }
            
            if (annotation.IsSelected)
                DrawSelectionIndicator(annotation);
        }
    }

    private UIElement? CreateShape(Annotation annotation)
    {
        var brush = new SolidColorBrush(annotation.StrokeColor);
        
        return annotation switch
        {
            ArrowAnnotation arrow => CreateArrowShape(arrow, brush),
            LineAnnotation line => CreateLineShape(line, brush),
            RectangleAnnotation rect => CreateRectangleShape(rect, brush),
            EllipseAnnotation ellipse => CreateEllipseShape(ellipse, brush),
            TextAnnotation text => CreateTextShape(text, brush),
            StepAnnotation step => CreateStepShape(step),
            HighlighterAnnotation hl => CreateHighlighterShape(hl),
            FilledRectAnnotation filled => CreateFilledRectShape(filled),
            MosaicAnnotation mosaic => CreateMosaicShape(mosaic),
            BlurAnnotation blur => CreateBlurShape(blur),
            MagnifierAnnotation mag => CreateMagnifierShape(mag),
            _ => null
        };
    }

    private UIElement CreateArrowShape(ArrowAnnotation arrow, SolidColorBrush brush)
    {
        var group = new GeometryGroup();
        group.Children.Add(new LineGeometry(arrow.StartPoint, arrow.EndPoint));
        var (h1, h2) = arrow.GetArrowHeadPoints();
        group.Children.Add(new LineGeometry(arrow.EndPoint, h1));
        group.Children.Add(new LineGeometry(arrow.EndPoint, h2));
        
        return new Path
        {
            Data = group, Stroke = brush, StrokeThickness = arrow.StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
    }

    private UIElement CreateLineShape(LineAnnotation line, SolidColorBrush brush)
    {
        return new System.Windows.Shapes.Line
        {
            X1 = line.StartPoint.X, Y1 = line.StartPoint.Y,
            X2 = line.EndPoint.X, Y2 = line.EndPoint.Y,
            Stroke = brush, StrokeThickness = line.StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
    }

    private UIElement CreateRectangleShape(RectangleAnnotation rect, SolidColorBrush brush)
    {
        var bounds = rect.GetBounds();
        var shape = new Rectangle
        {
            Width = Math.Abs(bounds.Width), Height = Math.Abs(bounds.Height),
            Stroke = brush, StrokeThickness = rect.StrokeThickness, Fill = Brushes.Transparent
        };
        Canvas.SetLeft(shape, bounds.X);
        Canvas.SetTop(shape, bounds.Y);
        return shape;
    }

    private UIElement CreateEllipseShape(EllipseAnnotation ellipse, SolidColorBrush brush)
    {
        var bounds = ellipse.GetBounds();
        var shape = new Ellipse
        {
            Width = Math.Abs(bounds.Width), Height = Math.Abs(bounds.Height),
            Stroke = brush, StrokeThickness = ellipse.StrokeThickness, Fill = Brushes.Transparent
        };
        Canvas.SetLeft(shape, bounds.X);
        Canvas.SetTop(shape, bounds.Y);
        return shape;
    }

    private UIElement CreateTextShape(TextAnnotation text, SolidColorBrush brush)
    {
        var textBlock = new TextBlock
        {
            Text = text.Text, Foreground = brush,
            FontSize = text.FontSize, FontWeight = text.FontWeight
        };
        Canvas.SetLeft(textBlock, text.Position.X);
        Canvas.SetTop(textBlock, text.Position.Y);
        return textBlock;
    }

    private UIElement CreateStepShape(StepAnnotation step)
    {
        var grid = new Grid { Width = step.Size, Height = step.Size };
        
        var ellipse = new Ellipse
        {
            Fill = new SolidColorBrush(step.StrokeColor),
            Width = step.Size, Height = step.Size
        };
        
        var text = new TextBlock
        {
            Text = step.StepNumber.ToString(),
            Foreground = Brushes.White,
            FontSize = step.Size * 0.5,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        grid.Children.Add(ellipse);
        grid.Children.Add(text);
        
        Canvas.SetLeft(grid, step.Position.X - step.Size / 2);
        Canvas.SetTop(grid, step.Position.Y - step.Size / 2);
        
        return grid;
    }

    private UIElement CreateHighlighterShape(HighlighterAnnotation hl)
    {
        if (hl.Points.Count < 2) return new Canvas();
        
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(hl.Points[0], false, false);
            ctx.PolyLineTo(hl.Points.Skip(1).ToList(), true, true);
        }
        
        return new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(hl.HighlightColor),
            StrokeThickness = hl.HighlightWidth,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
    }

    private UIElement CreateFilledRectShape(FilledRectAnnotation filled)
    {
        var bounds = filled.GetBounds();
        var shape = new Rectangle
        {
            Width = Math.Abs(bounds.Width), Height = Math.Abs(bounds.Height),
            Fill = new SolidColorBrush(filled.FillColor)
        };
        Canvas.SetLeft(shape, bounds.X);
        Canvas.SetTop(shape, bounds.Y);
        return shape;
    }

    private UIElement CreateMosaicShape(MosaicAnnotation mosaic)
    {
        var region = _pixelationRenderer.Render(_capturedImage, mosaic, mosaic.GetBounds(), mosaic.BlockSize);
        if (region == null) return new Canvas();

        var image = new Image
        {
            Source = region.Image,
            Width = region.Bounds.Width,
            Height = region.Bounds.Height,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(image, region.Bounds.X);
        Canvas.SetTop(image, region.Bounds.Y);
        return image;
    }

    private UIElement CreateBlurShape(BlurAnnotation blur)
    {
        var bounds = blur.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return new Canvas();
        
        // Create a cropped image for the blur region
        int x = Math.Clamp((int)bounds.X, 0, _capturedImage.PixelWidth - 1);
        int y = Math.Clamp((int)bounds.Y, 0, _capturedImage.PixelHeight - 1);
        int w = Math.Clamp((int)bounds.Width, 1, _capturedImage.PixelWidth - x);
        int h = Math.Clamp((int)bounds.Height, 1, _capturedImage.PixelHeight - y);
        
        var cropped = new CroppedBitmap(_capturedImage, new Int32Rect(x, y, w, h));
        
        var image = new Image
        {
            Source = cropped,
            Width = w, Height = h,
            Stretch = Stretch.Fill,
            Effect = new BlurEffect { Radius = blur.BlurRadius }
        };
        
        Canvas.SetLeft(image, bounds.X);
        Canvas.SetTop(image, bounds.Y);
        return image;
    }

    private void RenderSpotlight(SpotlightAnnotation spotlight)
    {
        var bounds = spotlight.GetBounds();
        var canvasWidth = _capturedImage.PixelWidth;
        var canvasHeight = _capturedImage.PixelHeight;
        var darkBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * spotlight.DarknessOpacity), 0, 0, 0));
        
        // Top
        if (bounds.Y > 0)
        {
            var top = new Rectangle { Width = canvasWidth, Height = bounds.Y, Fill = darkBrush };
            Canvas.SetLeft(top, 0); Canvas.SetTop(top, 0);
            EditorCanvas.Children.Add(top);
        }
        // Bottom
        if (bounds.Y + bounds.Height < canvasHeight)
        {
            var bottom = new Rectangle { Width = canvasWidth, Height = canvasHeight - bounds.Y - bounds.Height, Fill = darkBrush };
            Canvas.SetLeft(bottom, 0); Canvas.SetTop(bottom, bounds.Y + bounds.Height);
            EditorCanvas.Children.Add(bottom);
        }
        // Left
        if (bounds.X > 0)
        {
            var left = new Rectangle { Width = bounds.X, Height = bounds.Height, Fill = darkBrush };
            Canvas.SetLeft(left, 0); Canvas.SetTop(left, bounds.Y);
            EditorCanvas.Children.Add(left);
        }
        // Right
        if (bounds.X + bounds.Width < canvasWidth)
        {
            var right = new Rectangle { Width = canvasWidth - bounds.X - bounds.Width, Height = bounds.Height, Fill = darkBrush };
            Canvas.SetLeft(right, bounds.X + bounds.Width); Canvas.SetTop(right, bounds.Y);
            EditorCanvas.Children.Add(right);
        }
    }

    private UIElement CreateMagnifierShape(MagnifierAnnotation mag)
    {
        var displayBounds = mag.GetBounds();
        var displaySize = displayBounds.Width;
        
        // Create cropped/zoomed image
        int srcX = Math.Clamp((int)(mag.SourceCenter.X - mag.SourceRadius), 0, _capturedImage.PixelWidth - 1);
        int srcY = Math.Clamp((int)(mag.SourceCenter.Y - mag.SourceRadius), 0, _capturedImage.PixelHeight - 1);
        int srcW = Math.Clamp((int)(mag.SourceRadius * 2), 1, _capturedImage.PixelWidth - srcX);
        int srcH = Math.Clamp((int)(mag.SourceRadius * 2), 1, _capturedImage.PixelHeight - srcY);
        
        var cropped = new CroppedBitmap(_capturedImage, new Int32Rect(srcX, srcY, srcW, srcH));
        
        var grid = new Grid { Width = displaySize, Height = displaySize };
        
        // Circular clip
        grid.Clip = new EllipseGeometry(new Point(displaySize / 2, displaySize / 2), displaySize / 2, displaySize / 2);
        
        var image = new Image
        {
            Source = cropped,
            Width = displaySize, Height = displaySize,
            Stretch = Stretch.Fill
        };
        grid.Children.Add(image);
        
        // Border
        var border = new Ellipse
        {
            Width = displaySize, Height = displaySize,
            Stroke = new SolidColorBrush(mag.StrokeColor),
            StrokeThickness = 3,
            Fill = Brushes.Transparent
        };
        grid.Children.Add(border);
        
        Canvas.SetLeft(grid, displayBounds.X);
        Canvas.SetTop(grid, displayBounds.Y);
        return grid;
    }

    private void DrawSelectionIndicator(Annotation annotation)
    {
        var bounds = annotation.GetBounds();
        bounds.Inflate(4, 4);
        
        var rect = new Rectangle
        {
            Width = bounds.Width, Height = bounds.Height,
            Stroke = Brushes.DodgerBlue, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(rect, bounds.X);
        Canvas.SetTop(rect, bounds.Y);
        EditorCanvas.Children.Add(rect);
    }

    #endregion

    #region Keyboard Shortcuts

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var shortcuts = _settingsService?.Settings.EditorShortcuts ?? new EditorShortcutSettings();

        if (MatchesShortcut(e, shortcuts.Copy))
        {
            await CopyToClipboard();
            e.Handled = true;
            return;
        }
        if (MatchesShortcut(e, shortcuts.Save))
        {
            SaveToFile();
            e.Handled = true;
            return;
        }
        if (MatchesShortcut(e, shortcuts.Ocr))
        {
            OcrButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (MatchesShortcut(e, shortcuts.Translate))
        {
            OcrTranslateQuickButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (MatchesShortcut(e, shortcuts.Close))
        {
            Close();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z: _undoRedo.Undo(); RedrawAnnotations(); e.Handled = true; break;
                case Key.Y: _undoRedo.Redo(); RedrawAnnotations(); e.Handled = true; break;
                case Key.X:
                    if (await CopyToClipboard())
                        Close();
                    e.Handled = true;
                    break;
                case Key.W:
                    if (SaveToFile())
                        Close();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.Delete && _selectedAnnotation != null)
        {
            if (_selectedAnnotation is StepAnnotation) RecalculateStepNumbers();
            _undoRedo.Execute(new RemoveAnnotationAction(_annotations, _selectedAnnotation));
            _selectedAnnotation = null;
            RedrawAnnotations();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_isCropMode) CancelCropMode();
            else SelectAnnotation(null);
            e.Handled = true;
        }
        else
        {
            // Tool shortcuts
            switch (e.Key)
            {
                case Key.Space: PanToolButton.IsChecked = true; e.Handled = true; break;
                case Key.V: SelectToolButton.IsChecked = true; e.Handled = true; break;
                case Key.A: ArrowToolButton.IsChecked = true; e.Handled = true; break;
                case Key.L: LineToolButton.IsChecked = true; e.Handled = true; break;
                case Key.R: RectToolButton.IsChecked = true; e.Handled = true; break;
                case Key.E: EllipseToolButton.IsChecked = true; e.Handled = true; break;
                case Key.T: TextToolButton.IsChecked = true; e.Handled = true; break;
                case Key.N: StepToolButton.IsChecked = true; e.Handled = true; break;
                case Key.H: HighlighterToolButton.IsChecked = true; e.Handled = true; break;
                case Key.F: FilledRectToolButton.IsChecked = true; e.Handled = true; break;
                case Key.M: MosaicToolButton.IsChecked = true; e.Handled = true; break;
                case Key.B: BlurToolButton.IsChecked = true; e.Handled = true; break;
                case Key.S: SpotlightToolButton.IsChecked = true; e.Handled = true; break;
                case Key.G: MagnifierToolButton.IsChecked = true; e.Handled = true; break;
            }
        }
    }

    private void RecalculateStepNumbers()
    {
        var steps = _annotations.OfType<StepAnnotation>().OrderBy(s => s.StepNumber).ToList();
        for (int i = 0; i < steps.Count; i++)
            steps[i].StepNumber = i + 1;
        _nextStepNumber = steps.Count + 1;
        UpdateStatus();
    }

    private static bool MatchesShortcut(KeyEventArgs e, string? shortcut)
    {
        if (!ShortcutUtilities.TryParse(shortcut, out var modifiers, out var key))
            return false;

        return Keyboard.Modifiers == modifiers && e.Key == key;
    }

    #endregion

    #region Undo/Redo

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        _undoRedo.Undo();
        RecalculateStepNumbers();
        RedrawAnnotations();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        _undoRedo.Redo();
        RecalculateStepNumbers();
        RedrawAnnotations();
    }

    #endregion

    #region Save/Copy

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await CopyToClipboard();
    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveToFile();

    private async void OcrButton_Click(object sender, RoutedEventArgs e) => await RunOcrPipelineAsync(autoTranslate: false);

    private async Task RunOcrPipelineAsync(bool autoTranslate)
    {
        OcrButton.IsEnabled = false;
        OcrTranslateQuickButton.IsEnabled = false;
        StatusText.Text = L10n.Get("OcrRunning");

        try
        {
            var bitmap = RenderToBitmap();
            var text = await OcrService.RecognizeTextAsync(bitmap, _settingsService?.Settings.OcrLanguages);

            if (string.IsNullOrWhiteSpace(text))
            {
                // Keep the pane open (empty) so it doesn't vanish when a capture has no readable text.
                ShowOcrPane(string.Empty);
                StatusText.Text = L10n.Get("OcrNoTextFound");
                return;
            }

            UpdateStatus();
            ShowOcrPane(text);

            // Only follow up with translation if Ollama is actually configured; otherwise leave
            // the plain OCR result showing rather than popping an error for an unconfigured feature.
            if (autoTranslate
                && !string.IsNullOrWhiteSpace(_settingsService?.Settings.OllamaEndpoint)
                && !string.IsNullOrWhiteSpace(_settingsService?.Settings.OllamaModel))
            {
                await TranslateOcrPaneAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{L10n.Get("OcrFailed")}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus();
        }
        finally
        {
            OcrButton.IsEnabled = true;
            OcrTranslateQuickButton.IsEnabled = true;
        }
    }

    private void ShowOcrPane(string recognizedText)
    {
        OcrResultTextBox.Text = recognizedText;

        var savedLanguage = _settingsService?.Settings.TranslationTargetLanguage;
        if (!string.IsNullOrWhiteSpace(savedLanguage))
            OcrTargetLanguageComboBox.Text = savedLanguage;
        else if (OcrTargetLanguageComboBox.SelectedIndex < 0 && OcrTargetLanguageComboBox.Items.Count > 0)
            OcrTargetLanguageComboBox.SelectedIndex = 0;

        OcrPaneSplitter.Visibility = Visibility.Visible;
        OcrPane.Visibility = Visibility.Visible;
        OcrSplitterColumnDef.Width = new GridLength(4);
        OcrPaneColumnDef.Width = new GridLength(340);
    }

    private void HideOcrPane()
    {
        _ocrTranslateCts?.Cancel();

        OcrPane.Visibility = Visibility.Collapsed;
        OcrPaneSplitter.Visibility = Visibility.Collapsed;
        OcrSplitterColumnDef.Width = new GridLength(0);
        OcrPaneColumnDef.Width = new GridLength(0);
    }

    private void OcrPaneClose_Click(object sender, RoutedEventArgs e) => HideOcrPane();

    private const double MinOcrFontSize = 8;
    private const double MaxOcrFontSize = 36;

    private void OcrResultTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var delta = e.Delta > 0 ? 1 : -1;
        OcrResultTextBox.FontSize = Math.Clamp(OcrResultTextBox.FontSize + delta, MinOcrFontSize, MaxOcrFontSize);
        e.Handled = true;
    }

    private async void OcrCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(OcrResultTextBox.Text);
        }
        catch
        {
            // Clipboard access can fail transiently if another process holds it; ignore.
            return;
        }

        var original = OcrCopyButton.Content;
        OcrCopyButton.Content = "✓";
        await Task.Delay(800);
        OcrCopyButton.Content = original;
    }

    private async void OcrTranslateQuickButton_Click(object sender, RoutedEventArgs e) => await RunOcrPipelineAsync(autoTranslate: true);

    private CancellationTokenSource? _ocrTranslateCts;

    private async void OcrTranslate_Click(object sender, RoutedEventArgs e) => await TranslateOcrPaneAsync();

    private async Task TranslateOcrPaneAsync()
    {
        if (_ocrTranslateCts != null)
        {
            // A translation is already running; treat this click as "Cancel".
            _ocrTranslateCts.Cancel();
            return;
        }

        var targetLanguage = (OcrTargetLanguageComboBox.SelectedItem as ComboBoxItem)?.Content as string
            ?? OcrTargetLanguageComboBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(targetLanguage) || string.IsNullOrWhiteSpace(OcrResultTextBox.Text))
            return;

        var endpoint = _settingsService?.Settings.OllamaEndpoint;
        var model = _settingsService?.Settings.OllamaModel;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model))
        {
            MessageBox.Show(L10n.Get("OllamaNotConfigured"), L10n.Get("OcrResultTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ocrTranslateCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var originalContent = OcrTranslateButton.Content;
        OcrTranslateButton.Content = L10n.Get("Cancel");
        OcrTargetLanguageComboBox.IsEnabled = false;

        try
        {
            var translated = await OllamaTranslationService.TranslateAsync(
                OcrResultTextBox.Text, targetLanguage, endpoint, model, _ocrTranslateCts.Token);

            if (!string.IsNullOrWhiteSpace(translated))
            {
                OcrResultTextBox.Text = translated;

                if (_settingsService != null)
                {
                    _settingsService.Settings.TranslationTargetLanguage = targetLanguage;
                    _settingsService.Save();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Either the user clicked Cancel or the safety timeout elapsed; no error dialog needed.
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{L10n.Get("TranslationFailed")}: {ex.Message}", L10n.Get("OcrResultTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            OcrTranslateButton.Content = originalContent;
            OcrTargetLanguageComboBox.IsEnabled = true;
            _ocrTranslateCts?.Dispose();
            _ocrTranslateCts = null;
        }
    }

    private async Task<bool> CopyToClipboard()
    {
        try
        {
            var bitmap = RenderToBitmap();
            Clipboard.SetImage(bitmap);
            StatusText.Text = L10n.Get("CopiedToClipboard");
            
            // Show visual feedback on button
            CopyButton.Content = "✓";
            CopyButton.Background = new SolidColorBrush(Color.FromRgb(0, 128, 0)); // Green
            
            await Task.Delay(800);
            
            CopyButton.Content = L10n.Get("Copy");
            CopyButton.Background = new SolidColorBrush(Colors.Transparent);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{L10n.Get("CopyFailed")}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private string GetSaveDirectory()
    {
        return string.IsNullOrWhiteSpace(_settingsService?.Settings.SaveDirectory)
            ? SettingsService.DefaultSaveDirectory
            : _settingsService.Settings.SaveDirectory;
    }

    private void OpenSaveDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDirectory = System.IO.Path.GetFullPath(GetSaveDirectory());
            System.IO.Directory.CreateDirectory(saveDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Open save directory failed: {ex}");
            MessageBox.Show(this, $"{L10n.Get("OpenSaveDirectoryFailed")}: {ex.Message}",
                L10n.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool SaveToFile()
    {
        try
        {
            var saveDirectory = GetSaveDirectory();

            System.IO.Directory.CreateDirectory(saveDirectory);

            var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
            var filePath = System.IO.Path.Combine(saveDirectory, fileName);

            var bitmap = RenderToBitmap();
            SaveBitmapToFile(bitmap, filePath);
            StatusText.Text = string.Format(L10n.Get("Saved"), filePath);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{L10n.Get("SaveFailed")}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private RenderTargetBitmap RenderToBitmap()
    {
        var wasSelected = _selectedAnnotation;
        if (wasSelected != null)
        {
            wasSelected.IsSelected = false;
            RedrawAnnotations();
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(_capturedImage, new Rect(0, 0, _capturedImage.PixelWidth, _capturedImage.PixelHeight));
            foreach (var annotation in _annotations)
                DrawAnnotationToContext(context, annotation);
        }

        var bitmap = new RenderTargetBitmap(_capturedImage.PixelWidth, _capturedImage.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        if (wasSelected != null)
        {
            wasSelected.IsSelected = true;
            RedrawAnnotations();
        }

        return bitmap;
    }

    private void DrawAnnotationToContext(DrawingContext context, Annotation annotation)
    {
        var pen = new Pen(new SolidColorBrush(annotation.StrokeColor), annotation.StrokeThickness)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round
        };

        switch (annotation)
        {
            case ArrowAnnotation arrow:
                context.DrawLine(pen, arrow.StartPoint, arrow.EndPoint);
                var (h1, h2) = arrow.GetArrowHeadPoints();
                context.DrawLine(pen, arrow.EndPoint, h1);
                context.DrawLine(pen, arrow.EndPoint, h2);
                break;
            case LineAnnotation line:
                context.DrawLine(pen, line.StartPoint, line.EndPoint);
                break;
            case RectangleAnnotation rect:
                context.DrawRectangle(null, pen, rect.GetBounds());
                break;
            case EllipseAnnotation ellipse:
                var b = ellipse.GetBounds();
                context.DrawEllipse(null, pen, new Point(b.X + b.Width / 2, b.Y + b.Height / 2), b.Width / 2, b.Height / 2);
                break;
            case TextAnnotation text:
                var ft = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily(text.FontFamily), FontStyles.Normal, text.FontWeight, FontStretches.Normal),
                    text.FontSize, new SolidColorBrush(text.StrokeColor), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                context.DrawText(ft, text.Position);
                break;
            case StepAnnotation step:
                context.DrawEllipse(new SolidColorBrush(step.StrokeColor), null, step.Position, step.Size / 2, step.Size / 2);
                var stepText = new FormattedText(step.StepNumber.ToString(), System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight, new Typeface("Segoe UI"), step.Size * 0.5, Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                context.DrawText(stepText, new Point(step.Position.X - stepText.Width / 2, step.Position.Y - stepText.Height / 2));
                break;
            case HighlighterAnnotation hl:
                if (hl.Points.Count >= 2)
                {
                    var hlPen = new Pen(new SolidColorBrush(hl.HighlightColor), hl.HighlightWidth)
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    for (int i = 1; i < hl.Points.Count; i++)
                        context.DrawLine(hlPen, hl.Points[i - 1], hl.Points[i]);
                }
                break;
            case FilledRectAnnotation filled:
                context.DrawRectangle(new SolidColorBrush(filled.FillColor), null, filled.GetBounds());
                break;
            case MosaicAnnotation mosaic:
                DrawMosaicToContext(context, mosaic);
                break;
            case BlurAnnotation blur:
                DrawBlurToContext(context, blur);
                break;
            case SpotlightAnnotation spotlight:
                DrawSpotlightToContext(context, spotlight);
                break;
            case MagnifierAnnotation mag:
                DrawMagnifierToContext(context, mag);
                break;
        }
    }

    private void DrawMosaicToContext(DrawingContext context, MosaicAnnotation mosaic)
    {
        DrawPixelatedToContext(context, mosaic, mosaic.BlockSize);
    }

    private void DrawBlurToContext(DrawingContext context, BlurAnnotation blur)
    {
        // For export, we approximate blur with mosaic
        int blockSize = Math.Max(3, (int)(blur.BlurRadius / 2));
        DrawPixelatedToContext(context, blur, blockSize);
    }

    private void DrawPixelatedToContext(DrawingContext context, Annotation annotation, int blockSize)
    {
        var region = _pixelationRenderer.Render(_capturedImage, annotation, annotation.GetBounds(), blockSize);
        if (region != null)
            context.DrawImage(region.Image, region.Bounds);
    }

    private void DrawSpotlightToContext(DrawingContext context, SpotlightAnnotation spotlight)
    {
        var bounds = spotlight.GetBounds();
        var darkBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * spotlight.DarknessOpacity), 0, 0, 0));
        var canvasWidth = _capturedImage.PixelWidth;
        var canvasHeight = _capturedImage.PixelHeight;
        
        if (bounds.Y > 0)
            context.DrawRectangle(darkBrush, null, new Rect(0, 0, canvasWidth, bounds.Y));
        if (bounds.Y + bounds.Height < canvasHeight)
            context.DrawRectangle(darkBrush, null, new Rect(0, bounds.Y + bounds.Height, canvasWidth, canvasHeight - bounds.Y - bounds.Height));
        if (bounds.X > 0)
            context.DrawRectangle(darkBrush, null, new Rect(0, bounds.Y, bounds.X, bounds.Height));
        if (bounds.X + bounds.Width < canvasWidth)
            context.DrawRectangle(darkBrush, null, new Rect(bounds.X + bounds.Width, bounds.Y, canvasWidth - bounds.X - bounds.Width, bounds.Height));
    }

    private void DrawMagnifierToContext(DrawingContext context, MagnifierAnnotation mag)
    {
        var displayBounds = mag.GetBounds();
        var displaySize = displayBounds.Width;
        var center = new Point(displayBounds.X + displaySize / 2, displayBounds.Y + displaySize / 2);
        
        // Clip to circle
        context.PushClip(new EllipseGeometry(center, displaySize / 2, displaySize / 2));
        
        int srcX = Math.Clamp((int)(mag.SourceCenter.X - mag.SourceRadius), 0, _capturedImage.PixelWidth - 1);
        int srcY = Math.Clamp((int)(mag.SourceCenter.Y - mag.SourceRadius), 0, _capturedImage.PixelHeight - 1);
        int srcW = Math.Clamp((int)(mag.SourceRadius * 2), 1, _capturedImage.PixelWidth - srcX);
        int srcH = Math.Clamp((int)(mag.SourceRadius * 2), 1, _capturedImage.PixelHeight - srcY);
        
        var cropped = new CroppedBitmap(_capturedImage, new Int32Rect(srcX, srcY, srcW, srcH));
        context.DrawImage(cropped, displayBounds);
        
        context.Pop();
        
        // Border
        context.DrawEllipse(null, new Pen(new SolidColorBrush(mag.StrokeColor), 3), center, displaySize / 2, displaySize / 2);
    }

    private void SaveBitmapToFile(BitmapSource bitmap, string filePath)
    {
        BitmapEncoder encoder = System.IO.Path.GetExtension(filePath).ToLower() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            _ => new PngBitmapEncoder()
        };
        
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }

    #endregion
}
