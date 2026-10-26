using System.Text;
using System.Text.RegularExpressions;
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

        var (pixels, width, height) = GetBgraPixels(bitmapSource);

        using var softwareBitmap = await ToSoftwareBitmapAsync(pixels, width, height);
        var (bestText, bestScore) = await RecognizeBestAsync(engines, softwareBitmap);

        // Colored text over a similarly bright colored background (e.g. game subtitles) often
        // yields nothing from the original image. Only then retry with a saturation-based
        // black-on-white rendering, so ordinary captures keep their original result.
        if (bestScore < MinScoreBeforeFallback)
        {
            var fallbackPixels = BinarizeBySaturation(pixels);
            using var fallbackBitmap = await ToSoftwareBitmapAsync(fallbackPixels, width, height);
            var (fallbackText, fallbackScore) = await RecognizeBestAsync(engines, fallbackBitmap);

            DiagnosticLog.Write($"OCR saturation fallback size={width}x{height} originalScore={bestScore} fallbackScore={fallbackScore}");

            if (fallbackScore > bestScore)
            {
                bestScore = fallbackScore;
                bestText = fallbackText;
            }
        }

        return bestText;
    }

    /// <summary>Results with fewer recognized letters/digits than this trigger the saturation fallback.</summary>
    private const int MinScoreBeforeFallback = 20;

    private static async Task<(string Text, int Score)> RecognizeBestAsync(List<OcrEngine> engines, SoftwareBitmap softwareBitmap)
    {
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
            catch (Exception ex)
            {
                DiagnosticLog.Write($"OCR engine {engine.RecognizerLanguage.LanguageTag} failed: {ex.Message}");
                continue;
            }

            var text = BuildStructuredText(result, engine.RecognizerLanguage.LanguageTag);
            var score = text.Count(char.IsLetterOrDigit);

            if (score > bestScore)
            {
                bestScore = score;
                bestText = text;
            }
        }

        return (bestText, bestScore);
    }

    /// <summary>
    /// Renders the image as black text on white by separating pixels on color saturation.
    /// Otsu's threshold is applied twice: the first split separates dark/dull areas, and the
    /// second split of only the saturated side separates vivid text from a duller colored
    /// background whose brightness is too close to the text for a grayscale threshold.
    /// </summary>
    private static byte[] BinarizeBySaturation(byte[] bgra)
    {
        int pixelCount = bgra.Length / 4;
        var saturation = new byte[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            int b = bgra[i * 4], g = bgra[i * 4 + 1], r = bgra[i * 4 + 2];
            saturation[i] = (byte)(Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)));
        }

        var histogram = new int[256];
        foreach (var value in saturation)
            histogram[value]++;

        int firstThreshold = OtsuThreshold(histogram, 0);
        int threshold = OtsuThreshold(histogram, firstThreshold + 1);

        var result = new byte[bgra.Length];
        for (int i = 0; i < pixelCount; i++)
        {
            byte value = saturation[i] > threshold ? (byte)0 : (byte)255;
            result[i * 4] = result[i * 4 + 1] = result[i * 4 + 2] = value;
            result[i * 4 + 3] = 255;
        }

        return result;
    }

    /// <summary>
    /// Otsu's threshold over histogram bins from <paramref name="start"/> to 255. Pixels above the
    /// returned value form the foreground class.
    /// </summary>
    private static int OtsuThreshold(int[] histogram, int start)
    {
        long total = 0;
        double sum = 0;
        for (int t = start; t < 256; t++)
        {
            total += histogram[t];
            sum += (double)t * histogram[t];
        }

        int threshold = Math.Max(start - 1, 0);
        double sumBackground = 0, bestVariance = -1;
        long weightBackground = 0;

        for (int t = start; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0)
                continue;

            long weightForeground = total - weightBackground;
            if (weightForeground == 0)
                break;

            sumBackground += (double)t * histogram[t];
            double meanBackground = sumBackground / weightBackground;
            double meanForeground = (sum - sumBackground) / weightForeground;
            double variance = (double)weightBackground * weightForeground
                * (meanBackground - meanForeground) * (meanBackground - meanForeground);

            if (variance > bestVariance)
            {
                bestVariance = variance;
                threshold = t;
            }
        }

        return threshold;
    }

    /// <summary>
    /// Groups raw OCR lines into paragraphs and bullet/numbered list items, using the vertical
    /// gap between lines to tell a wrapped continuation from a genuine new paragraph — the
    /// Windows OCR API only returns a flat list of lines with no structure of its own.
    /// </summary>
    private static string BuildStructuredText(OcrResult result, string languageTag)
    {
        var lines = result.Lines;
        if (lines.Count == 0)
            return string.Empty;

        var blocks = new List<(List<string> Lines, bool IsBullet)>();
        List<string>? currentLines = null;
        bool currentIsBullet = false;
        LineBounds previousBounds = default;

        foreach (var line in lines)
        {
            var text = line.Text.Trim();
            if (text.Length == 0)
                continue;

            var bounds = GetLineBounds(line);
            var startsBullet = IsBulletLine(text);

            bool continuesPrevious = currentLines != null
                && !startsBullet
                && IsWrappedLine(previousBounds, bounds);

            if (continuesPrevious)
            {
                currentLines!.Add(text);
            }
            else
            {
                if (currentLines != null)
                    blocks.Add((currentLines, currentIsBullet));

                currentLines = [text];
                currentIsBullet = startsBullet;
            }

            previousBounds = bounds;
        }

        if (currentLines != null)
            blocks.Add((currentLines, currentIsBullet));

        var sb = new StringBuilder();
        for (int i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
                // Consecutive bullet/numbered items stay tight; anything else gets a blank
                // line between blocks so distinct paragraphs are visually separated.
                if (!(blocks[i].IsBullet && blocks[i - 1].IsBullet))
                    sb.AppendLine();
            }

            sb.Append(OcrTextFormatter.JoinLines(blocks[i].Lines, languageTag));
        }

        return sb.ToString();
    }

    private static readonly char[] BulletMarkers = ['•', '‣', '◦', '▪', '●', '○', '-', '*'];
    private static readonly Regex NumberedListRegex = new(@"^\(?[0-9]{1,3}[\.\)]\s|^\(?[a-zA-Z][\.\)]\s", RegexOptions.Compiled);

    private static bool IsBulletLine(string text)
    {
        if (text.Length == 0)
            return false;

        if (Array.IndexOf(BulletMarkers, text[0]) >= 0 && (text.Length == 1 || text[1] == ' '))
            return true;

        return NumberedListRegex.IsMatch(text);
    }

    private readonly record struct LineBounds(double Top, double Height);

    private static LineBounds GetLineBounds(OcrLine line)
    {
        if (line.Words.Count == 0)
            return new LineBounds(0, 0);

        double top = double.MaxValue, bottom = double.MinValue;
        foreach (var word in line.Words)
        {
            var rect = word.BoundingRect;
            top = Math.Min(top, rect.Y);
            bottom = Math.Max(bottom, rect.Y + rect.Height);
        }

        return new LineBounds(top, bottom - top);
    }

    /// <summary>
    /// True when the gap between two lines is small relative to their height (normal line
    /// spacing within a wrapped paragraph) rather than a deliberate paragraph break.
    /// </summary>
    private static bool IsWrappedLine(LineBounds previous, LineBounds current)
    {
        if (previous.Height <= 0 || current.Height <= 0)
            return false;

        double minHeight = Math.Min(previous.Height, current.Height);
        double maxHeight = Math.Max(previous.Height, current.Height);
        if (maxHeight / minHeight > 1.5)
            return false;

        double gap = current.Top - (previous.Top + previous.Height);
        double avgHeight = (previous.Height + current.Height) / 2.0;
        return gap < avgHeight * 0.6;
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

    private static (byte[] Pixels, int Width, int Height) GetBgraPixels(BitmapSource bitmapSource)
    {
        var converted = bitmapSource.Format == PixelFormats.Bgra32
            ? bitmapSource
            : new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        var pixels = new byte[height * stride];
        converted.CopyPixels(pixels, stride, 0);

        return (pixels, width, height);
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(byte[] pixels, int width, int height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
