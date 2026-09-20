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
        private readonly object lifecycleLock = new();
        private CancellationTokenSource cancellation;
        private Task monitorTask;
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

        public event Func<PalDetailsRegions, Task> DetailsChanged;
        public event Action<string> StatusChanged;

        public bool IsRunning => cancellation is { IsCancellationRequested: false };

        public void Start()
        {
            lock (lifecycleLock)
            {
                if (IsRunning || monitorTask is { IsCompleted: false }) return;

                cancellation = new CancellationTokenSource();
                var currentCancellation = cancellation;
                monitorTask = Task.Run(
                    () => MonitorLoop(currentCancellation, currentCancellation.Token),
                    currentCancellation.Token
                );
            }
        }

        public void Stop()
        {
            lock (lifecycleLock)
                cancellation?.Cancel();
            nextRecognitionAllowedUtc = DateTime.MinValue;
        }

        public void Dispose() => Stop();

        private async Task MonitorLoop(CancellationTokenSource owner, CancellationToken token)
        {
            StatusChanged?.Invoke("Palworld 창을 찾는 중");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Capturing the full DirectX window is the expensive step. Do it only
                        // when OCR is actually due instead of on every lightweight poll.
                        if (DateTime.UtcNow < nextRecognitionAllowedUtc)
                        {
                            await Task.Delay(interval, token);
                            continue;
                        }

                        nextRecognitionAllowedUtc = DateTime.UtcNow.AddSeconds(6);
                        var frame = capture.Capture();
                        var details = ScreenRegionExtractor.ExtractDetails(frame, profile);
                        var handler = DetailsChanged;
                        if (handler != null)
                            await handler(details);
                    }
                    catch (InvalidOperationException ex)
                    {
                        StatusChanged?.Invoke(ex.Message);
                    }

                    await Task.Delay(interval, token);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                lock (lifecycleLock)
                {
                    if (ReferenceEquals(cancellation, owner))
                    {
                        cancellation.Dispose();
                        cancellation = null;
                        monitorTask = null;
                    }
                }
            }
        }

    }
}
