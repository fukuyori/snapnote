using System;
using System.Windows;

namespace SnapNoteStudio.Services;

/// <summary>Maps physical desktop coordinates to pixels in a virtual-screen capture.</summary>
public static class CaptureCoordinates
{
    public static Int32Rect GetCropRect(Rect selection, Rect screenBounds, int pixelWidth, int pixelHeight)
    {
        if (selection.IsEmpty || screenBounds.IsEmpty || pixelWidth <= 0 || pixelHeight <= 0)
            return Int32Rect.Empty;

        // The bitmap origin is the captured desktop's top-left, which may be negative.
        // Intersect edges before calculating width/height; clamping only the origin
        // would enlarge a selection that starts outside the captured desktop.
        selection.Intersect(new Rect(screenBounds.X, screenBounds.Y, pixelWidth, pixelHeight));
        if (selection.IsEmpty)
            return Int32Rect.Empty;

        int left = (int)Math.Round(selection.Left - screenBounds.Left);
        int top = (int)Math.Round(selection.Top - screenBounds.Top);
        int right = (int)Math.Round(selection.Right - screenBounds.Left);
        int bottom = (int)Math.Round(selection.Bottom - screenBounds.Top);
        return right > left && bottom > top
            ? new Int32Rect(left, top, right - left, bottom - top)
            : Int32Rect.Empty;
    }

    public static Rect ToScreenRect(Int32Rect crop, Rect screenBounds) =>
        crop.IsEmpty ? Rect.Empty : new Rect(
            screenBounds.Left + crop.X, screenBounds.Top + crop.Y, crop.Width, crop.Height);
}
