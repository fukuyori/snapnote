using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;
using DrawingRect = System.Drawing.Rectangle;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImaging = System.Drawing.Imaging;

namespace SnapNoteStudio.Services;

public static class ScreenCaptureService
{
    /// <summary>
    /// Get virtual screen bounds in physical pixels (handles negative coordinates)
    /// </summary>
    public static System.Windows.Rect GetVirtualScreenBounds()
    {
        var virtualScreen = WinForms.SystemInformation.VirtualScreen;
        return new System.Windows.Rect(virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, virtualScreen.Height);
    }

    /// <summary>
    /// Capture the entire virtual screen (all monitors)
    /// </summary>
    public static BitmapSource? CaptureScreen()
    {
        var virtualScreen = WinForms.SystemInformation.VirtualScreen;
        
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
            return null;

        try
        {
            using var bitmap = new DrawingBitmap(virtualScreen.Width, virtualScreen.Height, DrawingImaging.PixelFormat.Format32bppArgb);
            using var graphics = DrawingGraphics.FromImage(bitmap);
            
            // CopyFromScreen handles negative coordinates correctly
            graphics.CopyFromScreen(
                virtualScreen.Left,  // Source X (can be negative)
                virtualScreen.Top,   // Source Y (can be negative)
                0,                   // Destination X
                0,                   // Destination Y
                virtualScreen.Size,
                System.Drawing.CopyPixelOperation.SourceCopy);
            
            return ConvertToBitmapSource(bitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen capture failed: {ex.Message}");
            return null;
        }
    }

    private static BitmapSource ConvertToBitmapSource(DrawingBitmap bitmap)
    {
        var bitmapData = bitmap.LockBits(
            new DrawingRect(0, 0, bitmap.Width, bitmap.Height),
            DrawingImaging.ImageLockMode.ReadOnly,
            DrawingImaging.PixelFormat.Format32bppArgb);

        var bitmapSource = BitmapSource.Create(
            bitmapData.Width,
            bitmapData.Height,
            96, 96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            bitmapData.Scan0,
            bitmapData.Stride * bitmapData.Height,
            bitmapData.Stride);

        bitmap.UnlockBits(bitmapData);
        bitmapSource.Freeze();
        return bitmapSource;
    }
}
