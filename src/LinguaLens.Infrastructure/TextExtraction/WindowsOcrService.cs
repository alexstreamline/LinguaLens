using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using LinguaLens.Core.Interfaces;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WpfPoint = System.Windows.Point;
using WpfRect  = System.Windows.Rect;
using WinLanguage = Windows.Globalization.Language;
using CoreOcrLine   = LinguaLens.Core.Models.OcrLine;
using CoreOcrResult = LinguaLens.Core.Models.OcrResult;

namespace LinguaLens.Infrastructure.TextExtraction;

/// <summary>
/// OCR-fallback на Windows.Media.Ocr (нативный Windows OCR API).
/// Захватывает регион 800×200 px вокруг курсора, распознаёт текст, ищет слово
/// под точкой и возвращает его + всю строку как контекст.
///
/// Требования: Windows 10 1809+ и установленный OCR pack для языка
/// (Settings → Time &amp; Language → Languages → Add language → Optional features → OCR).
/// </summary>
public sealed class WindowsOcrService : IOcrService
{
    private const int RegionWidth = 800;
    private const int RegionHeight = 200;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private readonly OcrEngine? _ocrEngine;

    public WindowsOcrService()
    {
        // Сначала пробуем системные языки, потом en-US, потом es-ES — что найдётся.
        _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new WinLanguage("en-US"))
                  ?? OcrEngine.TryCreateFromLanguage(new WinLanguage("es-ES"));
    }

    public async Task<CoreOcrResult?> ExtractTextNearAsync(WpfPoint screenPoint, CancellationToken ct)
    {
        if (_ocrEngine is null) return null;

        // Виртуальный десктоп (объединение всех мониторов).
        int vsLeft   = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vsTop    = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vsRight  = vsLeft + GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vsBottom = vsTop  + GetSystemMetrics(SM_CYVIRTUALSCREEN);

        int left = Math.Max(vsLeft, (int)screenPoint.X - RegionWidth / 2);
        int top  = Math.Max(vsTop,  (int)screenPoint.Y - RegionHeight / 2);
        int width  = Math.Min(RegionWidth,  vsRight  - left);
        int height = Math.Min(RegionHeight, vsBottom - top);
        if (width <= 0 || height <= 0) return null;

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
            }

            ct.ThrowIfCancellationRequested();

            // Bitmap → PNG в памяти → InMemoryRandomAccessStream → SoftwareBitmap.
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);

            using var randomStream = new InMemoryRandomAccessStream();
            var writer = new DataWriter(randomStream);
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            writer.DetachStream();
            randomStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(randomStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            ct.ThrowIfCancellationRequested();

            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
            if (ocrResult.Lines.Count == 0) return null;

            // Координаты курсора в системе изображения (origin = top-left региона).
            var localX = screenPoint.X - left;
            var localY = screenPoint.Y - top;

            string? wordAtPoint = null;
            var lines = new List<CoreOcrLine>(ocrResult.Lines.Count);

            foreach (var line in ocrResult.Lines)
            {
                if (line.Words.Count == 0) continue;

                // Bounds строки — union первого и последнего слова.
                var firstRect = line.Words[0].BoundingRect;
                var lastRect  = line.Words[^1].BoundingRect;
                var lineBounds = new WpfRect(
                    firstRect.X, firstRect.Y,
                    lastRect.X + lastRect.Width - firstRect.X,
                    Math.Max(firstRect.Height, lastRect.Height));
                lines.Add(new CoreOcrLine(line.Text, lineBounds));

                if (wordAtPoint != null) continue;

                foreach (var word in line.Words)
                {
                    var r = word.BoundingRect;
                    if (localX >= r.X && localX <= r.X + r.Width &&
                        localY >= r.Y && localY <= r.Y + r.Height)
                    {
                        wordAtPoint = StripPunctuation(word.Text);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(wordAtPoint)) return null;

            // Контекст — ВСЁ распознанное в регионе, по порядку сверху-вниз,
            // склеенное пробелами. Даёт LLM полноценный параграф вместо одной строки.
            var contextSentence = string.Join(" ", lines.Select(l => l.Text));

            return new CoreOcrResult(wordAtPoint, contextSentence, lines);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private static string StripPunctuation(string word)
    {
        var span = word.AsSpan();
        int start = 0, end = span.Length - 1;
        while (start <= end && char.IsPunctuation(span[start])) start++;
        while (end >= start && char.IsPunctuation(span[end])) end--;
        return start <= end ? word.Substring(start, end - start + 1) : "";
    }
}
