using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class WindowsKoreanOcr
    {
        private readonly OcrEngine engine;

        public WindowsKoreanOcr()
        {
            engine = OcrEngine.TryCreateFromLanguage(new Language("ko"))
                ?? throw new InvalidOperationException(
                    "Windows 한국어 OCR을 사용할 수 없습니다. Windows 언어 설정에서 한국어 기능을 설치해 주세요."
                );
        }

        public async Task<OcrTextResult> ReadAsync(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Palworld's detail labels are relatively small at 1080p. Enlarging them before
            // OCR improves Hangul syllable separation without changing the configured regions.
            var enlarged = new TransformedBitmap(source, new ScaleTransform(3, 3));
            enlarged.Freeze();

            using var encoded = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(enlarged));
            encoder.Save(encoded);
            encoded.Position = 0;

            using var randomAccess = new InMemoryRandomAccessStream();
            using (var output = randomAccess.AsStreamForWrite())
            {
                await encoded.CopyToAsync(output);
                await output.FlushAsync();
            }
            randomAccess.Seek(0);

            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccess);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied
            );
            var result = await engine.RecognizeAsync(softwareBitmap);
            var text = string.Join(" ", result.Lines.Select(line => line.Text)).Trim();

            return new OcrTextResult
            {
                Text = text,
                Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 0.95
            };
        }

        public async Task<(OcrTextResult Name, OcrTextResult[] Passives)> ReadDetailsAsync(PalDetailsRegions regions)
        {
            var nameTask = ReadAsync(regions.Name);
            var passiveTasks = regions.Passives.Select(ReadAsync).ToArray();
            await Task.WhenAll(passiveTasks.Append(nameTask));
            return (await nameTask, passiveTasks.Select(task => task.Result).ToArray());
        }
    }
}
