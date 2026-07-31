using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace SnapNoteStudio.Services;

public static class OcrService
{
    public static IReadOnlyList<Language> AvailableRecognizerLanguages => OcrEngine.AvailableRecognizerLanguages;

    public static async Task<string> RecognizeTextAsync(BitmapSource bitmapSource, IReadOnlyList<string>? languageTags = null)
    {
        var engines = new List<OcrEngine>();

        if (languageTags is { Count: > 0 })
        {
            foreach (var tag in languageTags)
            {
                if (TryCreateEngine(tag) is { } engine)
                    engines.Add(engine);
            }
        }

        if (engines.Count == 0 && OcrEngine.TryCreateFromUserProfileLanguages() is { } fallbackEngine)
            engines.Add(fallbackEngine);

        if (engines.Count == 0)
            return string.Empty;

        using var softwareBitmap = await ToSoftwareBitmapAsync(bitmapSource);

        // When multiple candidate languages are configured, the real language of the
        // captured text isn't known ahead of time, so every candidate is run and the
        // result with the most recognized letters/digits (i.e. the least garbled) wins.
        string bestText = string.Empty;
        int bestScore = -1;

        foreach (var engine in engines)
        {
            OcrResult result;
            try
            {
                result = await engine.RecognizeAsync(softwareBitmap);
            }
            catch
            {
                continue;
            }

            var text = result.Text ?? string.Empty;
            var score = text.Count(char.IsLetterOrDigit);

            if (score > bestScore)
            {
                bestScore = score;
                bestText = text;
            }
        }

        return bestText;
    }

    private static OcrEngine? TryCreateEngine(string languageTag)
    {
        try
        {
            return OcrEngine.TryCreateFromLanguage(new Language(languageTag));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(BitmapSource bitmapSource)
    {
        var converted = bitmapSource.Format == PixelFormats.Bgra32
            ? bitmapSource
            : new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        var pixels = new byte[height * stride];
        converted.CopyPixels(pixels, stride, 0);

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
