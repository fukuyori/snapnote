using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapNoteStudio.Services;

public class CaptureEventArgs : EventArgs
{
    public BitmapSource? CapturedImage { get; }
    /// <summary>Captured bounds in absolute physical desktop pixels.</summary>
    public Rect CaptureRegion { get; }

    public CaptureEventArgs(BitmapSource? image, Rect region)
    {
        CapturedImage = image;
        CaptureRegion = region;
    }
}
