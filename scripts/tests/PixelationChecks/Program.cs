using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapNoteStudio.Services;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main()
    {
        var source = MakeSource(41, 29);
        var renderer = new PixelationRenderer();
        foreach (var (bounds, block) in new[]
        {
            (new Rect(0, 0, 41, 29), 10),
            (new Rect(3, 2, 23, 17), 6),
            (new Rect(4, 7, 1, 1), 10),
            (new Rect(0, 0, 41, 29), 1),
            (new Rect(-4, -3, 15, 12), 4),
            (new Rect(37, 24, 20, 20), 3)
        })
        {
            var region = renderer.Render(source, new object(), bounds, block)!;
            var actual = Export(region, 41, 29);
            var expected = ReferencePixels(source, bounds, block);
            Check(Pixels(actual).SequenceEqual(expected), $"Block averages and clipping: {bounds}, block={block}");
            Check(Pixels(Preview(region, 41, 29)).SequenceEqual(Pixels(actual)), "Preview matches export");
        }

        var owner = new object();
        var area = new Rect(3, 2, 23, 17);
        var initial = renderer.Render(source, owner, area, 6)!;
        Check(ReferenceEquals(initial, renderer.Render(source, owner, area, 6)), "Reuse unchanged region");
        var moved = renderer.Render(source, owner, new Rect(5, 3, 23, 17), 6)!;
        Check(!ReferenceEquals(initial, moved), "Invalidate moved region");
        Check(Pixels(Export(moved, 41, 29)).SequenceEqual(ReferencePixels(source, moved.Bounds, 6)), "Moved region pixels");
        var resizedBlocks = renderer.Render(source, owner, moved.Bounds, 3)!;
        Check(!ReferenceEquals(moved, resizedBlocks), "Invalidate changed block size");
        Check(Pixels(Export(resizedBlocks, 41, 29)).SequenceEqual(ReferencePixels(source, moved.Bounds, 3)), "Changed block pixels");
        var other = renderer.Render(source, new object(), area, 6)!;
        Check(ReferenceEquals(resizedBlocks, renderer.Render(source, owner, moved.Bounds, 3)), "Independent annotations retain cache");

        var changedSource = MakeSource(41, 29, 70);
        var replaced = renderer.Render(changedSource, owner, area, 6)!;
        Check(Pixels(Export(replaced, 41, 29)).SequenceEqual(ReferencePixels(changedSource, area, 6)), "Image replacement invalidates source cache");
        Check(!Pixels(Export(replaced, 41, 29)).SequenceEqual(Pixels(Export(initial, 41, 29))), "New source changes mosaic colors");
        var restored = renderer.Render(source, owner, area, 6)!;
        Check(Pixels(Export(restored, 41, 29)).SequenceEqual(Pixels(Export(initial, 41, 29))), "Undo image replacement");
        renderer.Clear();
        Check(!ReferenceEquals(restored, renderer.Render(source, owner, area, 6)), "Explicit cache clear");
        Check(renderer.Render(source, owner, Rect.Empty, 6) == null, "Empty region");
        Check(renderer.Render(source, owner, new Rect(0, 0, 0, 10), 6) == null, "Zero width");
        Check(renderer.Render(source, owner, new Rect(100, 100, 5, 5), 6) == null, "Outside capture");
        bool rejectedZero = false;
        try { renderer.Render(source, owner, area, 0); }
        catch (ArgumentOutOfRangeException) { rejectedZero = true; }
        Check(rejectedZero, "Reject zero block size instead of looping indefinitely");

        var convertedSource = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
        var convertedRegion = renderer.Render(convertedSource, owner, area, 6)!;
        Check(Pixels(Export(convertedRegion, 41, 29)).SequenceEqual(Pixels(Export(initial, 41, 29))), "Non-BGRA source conversion");
        var tiny = renderer.Render(source, owner, new Rect(40.75, 28.75, 0.25, 0.25), 6)!;
        Check(tiny.Bounds == new Rect(40.75, 28.75, 0.25, 0.25), "Fractional edge bounds");
        Check(tiny.Image.IsFrozen, "Cached rendering is immutable");
        var fractional = renderer.Render(source, owner, new Rect(2.25, 3.5, 20.5, 15.25), 6)!;
        Check(Pixels(Preview(fractional, 41, 29)).SequenceEqual(Pixels(Export(fractional, 41, 29))), "Fractional preview/export agreement");

        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(Export(initial, 41, 29)));
        using (var stream = new System.IO.MemoryStream())
        {
            png.Save(stream);
            stream.Position = 0;
            var decoded = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Check(Pixels(decoded.Frames[0]).SequenceEqual(Pixels(Export(initial, 41, 29))), "Saved PNG pixels match rendered mosaic");
        }

        MeasureLargeCapture();
        Console.WriteLine($"All {_checks} pixelation checks passed.");
    }

    private static BitmapSource MakeSource(int width, int height, int offset = 0)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                pixels[i] = (byte)((x * 13 + y * 7 + offset) % 256);
                pixels[i + 1] = (byte)((x * 3 + y * 11 + offset) % 256);
                pixels[i + 2] = (byte)((x * 17 + y * 5 + offset) % 256);
                pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    // Independent, straightforward per-output-pixel reference for integer bounds.
    private static byte[] ReferencePixels(BitmapSource source, Rect bounds, int blockSize)
    {
        var input = Pixels(source);
        var output = new byte[input.Length];
        bounds.Intersect(new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        for (int y = (int)bounds.Top; y < bounds.Bottom; y++)
            for (int x = (int)bounds.Left; x < bounds.Right; x++)
            {
                int left = (int)bounds.Left + (x - (int)bounds.Left) / blockSize * blockSize;
                int top = (int)bounds.Top + (y - (int)bounds.Top) / blockSize * blockSize;
                int right = Math.Min(left + blockSize, (int)bounds.Right);
                int bottom = Math.Min(top + blockSize, (int)bounds.Bottom);
                for (int channel = 0; channel < 3; channel++)
                {
                    int sum = 0;
                    for (int sy = top; sy < bottom; sy++)
                        for (int sx = left; sx < right; sx++)
                            sum += input[(sy * source.PixelWidth + sx) * 4 + channel];
                    output[(y * source.PixelWidth + x) * 4 + channel] = (byte)(sum / ((right - left) * (bottom - top)));
                }
                output[(y * source.PixelWidth + x) * 4 + 3] = 255;
            }
        return output;
    }

    private static byte[] Pixels(BitmapSource source)
    {
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        var converted = source.Format == PixelFormats.Pbgra32 ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        converted.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }

    private static BitmapSource Export(PixelationRenderer.Region region, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawImage(region.Image, region.Bounds);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static BitmapSource Preview(PixelationRenderer.Region region, int width, int height)
    {
        var image = new Image { Source = region.Image, Width = region.Bounds.Width, Height = region.Bounds.Height, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(image, region.Bounds.X);
        Canvas.SetTop(image, region.Bounds.Y);
        var canvas = new Canvas { Width = width, Height = height };
        canvas.Children.Add(image);
        canvas.Measure(new Size(width, height));
        canvas.Arrange(new Rect(0, 0, width, height));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        return bitmap;
    }

    private static void MeasureLargeCapture()
    {
        var source = MakeSource(3840, 2160);
        var renderer = new PixelationRenderer();
        var owner = new object();
        var bounds = new Rect(0, 0, 3840, 2160);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var region = renderer.Render(source, owner, bounds, 6)!;
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"4K full-frame mosaic (block 6): {stopwatch.Elapsed.TotalMilliseconds:F1} ms, managed allocation {allocated / 1024.0 / 1024.0:F2} MiB");
        Check(allocated < 64L * 1024 * 1024, "4K allocations stay below two full source buffers");

        stopwatch.Restart();
        for (int i = 0; i < 1000; i++)
            if (!ReferenceEquals(region, renderer.Render(source, owner, bounds, 6)))
                throw new Exception("Large unchanged region was regenerated");
        stopwatch.Stop();
        Console.WriteLine($"1000 cached 4K requests: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        stopwatch.Restart();
        for (int i = 0; i < 30; i++)
            renderer.Render(source, owner, new Rect(i, i, 3840 - 2 * i, 2160 - 2 * i), 6);
        stopwatch.Stop();
        Console.WriteLine($"30 changing 4K selections: {stopwatch.Elapsed.TotalMilliseconds:F1} ms total, {stopwatch.Elapsed.TotalMilliseconds / 30:F1} ms/selection");

        stopwatch.Restart();
        var bitmap = Export(region, 3840, 2160);
        stopwatch.Stop();
        Console.WriteLine($"4K offscreen export render: {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        Check(bitmap.PixelWidth == 3840 && bitmap.PixelHeight == 2160, "4K export dimensions");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }
}
