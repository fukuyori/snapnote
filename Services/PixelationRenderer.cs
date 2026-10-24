using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapNoteStudio.Services;

/// <summary>
/// UI-thread renderer shared by the editor and export. Each output pixel represents
/// one averaged mosaic block; nearest-neighbor scaling preserves the block edges.
/// </summary>
public sealed class PixelationRenderer
{
    private BitmapSource? _source;
    private byte[] _pixels = Array.Empty<byte>();
    private int _stride;
    private ConditionalWeakTable<object, CachedRegion> _regions = new();

    public sealed record Region(Rect Bounds, DrawingImage Image);
    private sealed record CachedRegion(Rect Bounds, int BlockSize, Region Region);

    public void Clear()
    {
        _source = null;
        _pixels = Array.Empty<byte>();
        _regions = new();
    }

    public Region? Render(BitmapSource source, object annotation, Rect bounds, int blockSize)
    {
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize));

        // Source images are replaced, rather than edited in place, by the editor.
        if (!ReferenceEquals(source, _source))
        {
            Clear();
            var converted = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            _stride = checked(source.PixelWidth * 4);
            _pixels = new byte[checked(_stride * source.PixelHeight)];
            converted.CopyPixels(_pixels, _stride, 0);
            _source = source;
        }

        bounds.Intersect(new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        if (_regions.TryGetValue(annotation, out var cached)
            && cached.Bounds == bounds && cached.BlockSize == blockSize)
            return cached.Region;

        int columns = (int)Math.Ceiling(bounds.Width / blockSize);
        int rows = (int)Math.Ceiling(bounds.Height / blockSize);
        int outputStride = checked(columns * 4);
        var output = new byte[checked(outputStride * rows)];

        for (int row = 0; row < rows; row++)
        {
            int y = (int)(bounds.Y + row * blockSize);
            int height = Math.Min(source.PixelHeight - y,
                Math.Max(1, (int)Math.Min(blockSize, bounds.Height - row * blockSize)));
            for (int column = 0; column < columns; column++)
            {
                int x = (int)(bounds.X + column * blockSize);
                int width = Math.Min(source.PixelWidth - x,
                    Math.Max(1, (int)Math.Min(blockSize, bounds.Width - column * blockSize)));
                long red = 0, green = 0, blue = 0;
                for (int py = y; py < y + height; py++)
                {
                    int end = py * _stride + (x + width) * 4;
                    for (int index = py * _stride + x * 4; index < end; index += 4)
                    {
                        blue += _pixels[index];
                        green += _pixels[index + 1];
                        red += _pixels[index + 2];
                    }
                }

                long count = (long)width * height;
                int target = row * outputStride + column * 4;
                output[target] = (byte)(blue / count);
                output[target + 1] = (byte)(green / count);
                output[target + 2] = (byte)(red / count);
                // Match the existing opaque mosaic, even for transparent source pixels.
                output[target + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(columns, rows, 96, 96,
            PixelFormats.Bgra32, null, output, outputStride);
        bitmap.Freeze();

        var drawing = new DrawingGroup
        {
            ClipGeometry = new RectangleGeometry(new Rect(0, 0, bounds.Width, bounds.Height))
        };
        RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.NearestNeighbor);
        using (var context = drawing.Open())
        {
            // Stretch to whole blocks, then clip the last row/column. Stretching to
            // bounds directly would shrink every block when the last one is partial.
            context.DrawImage(bitmap, new Rect(0, 0, (double)columns * blockSize, (double)rows * blockSize));
        }
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        var region = new Region(bounds, image);
        _regions.Remove(annotation);
        _regions.Add(annotation, new CachedRegion(bounds, blockSize, region));
        return region;
    }
}
