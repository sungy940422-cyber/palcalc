using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class PalworldScreenMonitor : IDisposable
    {
        private readonly PalworldWindowCapture capture;
        private readonly ScreenRecognitionProfile profile;
        private readonly TimeSpan interval;
        private CancellationTokenSource cancellation;
        private ulong? previousFingerprint;

        public PalworldScreenMonitor(
            PalworldWindowCapture capture,
            ScreenRecognitionProfile profile,
            TimeSpan? interval = null)
        {
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.interval = interval ?? TimeSpan.FromMilliseconds(750);
        }

        public event Action<PalDetailsRegions> DetailsChanged;
        public event Action<string> StatusChanged;

        public bool IsRunning => cancellation != null;

        public void Start()
        {
            if (IsRunning) return;

            cancellation = new CancellationTokenSource();
            _ = MonitorLoop(cancellation.Token);
        }

        public void Stop()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            previousFingerprint = null;
        }

        public void Dispose() => Stop();

        private async Task MonitorLoop(CancellationToken token)
        {
            StatusChanged?.Invoke("Palworld 창을 찾는 중");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var frame = capture.Capture();
                    var details = ScreenRegionExtractor.ExtractDetails(frame, profile);
                    var fingerprint = Fingerprint(details.Name);

                    if (fingerprint != previousFingerprint)
                    {
                        previousFingerprint = fingerprint;
                        DetailsChanged?.Invoke(details);
                    }

                }
                catch (InvalidOperationException ex)
                {
                    StatusChanged?.Invoke(ex.Message);
                }

                try
                {
                    await Task.Delay(interval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private static ulong Fingerprint(BitmapSource source)
        {
            const int sampleWidth = 16;
            const int sampleHeight = 8;
            var resized = new TransformedBitmap(
                source,
                new ScaleTransform(sampleWidth / (double)source.PixelWidth, sampleHeight / (double)source.PixelHeight)
            );
            var converted = new FormatConvertedBitmap(resized, PixelFormats.Gray8, null, 0);
            var pixels = new byte[sampleWidth * sampleHeight];
            converted.CopyPixels(pixels, sampleWidth, 0);

            long sum = 0;
            foreach (var pixel in pixels) sum += pixel;
            var average = sum / pixels.Length;

            ulong result = 0;
            for (var i = 0; i < 64; i++)
            {
                var pairAverage = (pixels[i * 2] + pixels[i * 2 + 1]) / 2;
                if (pairAverage >= average)
                    result |= 1UL << i;
            }
            return result;
        }
    }
}
