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
        private DateTime nextRecognitionAllowedUtc;

        public PalworldScreenMonitor(
            PalworldWindowCapture capture,
            ScreenRecognitionProfile profile,
            TimeSpan? interval = null)
        {
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.interval = interval ?? TimeSpan.FromSeconds(2);
        }

        public event Action<PalDetailsRegions> DetailsChanged;
        public event Action<string> StatusChanged;

        public bool IsRunning => cancellation != null;

        public void Start()
        {
            if (IsRunning) return;

            cancellation = new CancellationTokenSource();
            _ = Task.Run(() => MonitorLoop(cancellation.Token), cancellation.Token);
        }

        public void Stop()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            nextRecognitionAllowedUtc = DateTime.MinValue;
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

                    // Poll at a fixed low rate. Depending on the graphics driver,
                    // fingerprints from PrintWindow/BitBlt can remain stale even after
                    // the game UI changes, so they must not gate recognition.
                    if (DateTime.UtcNow >= nextRecognitionAllowedUtc)
                    {
                        nextRecognitionAllowedUtc = DateTime.UtcNow.AddSeconds(6);
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

    }
}
