using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapNoteStudio.Services;

public class CaptureEventArgs : EventArgs
{
    public BitmapSource? CapturedImage { get; }
    public Rect CaptureRegion { get; }

    public CaptureEventArgs(BitmapSource? image, Rect region)
    {
        CapturedImage = image;
        CaptureRegion = region;
    }
}
