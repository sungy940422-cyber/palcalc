using PalCalc.Model;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class LiveRecognitionController : IDisposable
    {
        private readonly PalworldScreenMonitor monitor;
        private readonly LivePalRecognizer recognizer;
        private readonly Dictionary<string, DateTime> recentObservations = [];
        private int recognitionInProgress;
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);

        public LiveRecognitionController(PalDB database)
        {
            var profile = ScreenRecognitionProfileLoader.LoadKorean1080pBorderless();
            monitor = new PalworldScreenMonitor(new PalworldWindowCapture(), profile);
            recognizer = new LivePalRecognizer(
                new WindowsKoreanOcr(),
                new LivePalObservationFactory(new KoreanGameTextMatcher(database))
            );

            monitor.DetailsChanged += Monitor_DetailsChanged;
            monitor.StatusChanged += status => StatusChanged?.Invoke(status);
        }

        public ObservableCollection<LivePalObservation> ProvisionalPals { get; } = [];
        public bool IsRunning => monitor.IsRunning;

        public event Action<string> StatusChanged;
        public event Action<LivePalObservation> ObservationEvaluated;
        public event Action<LivePalObservation> PalRecognized;

        public void Start() => monitor.Start();
        public void Stop() => monitor.Stop();

        public void Dispose()
        {
            monitor.DetailsChanged -= Monitor_DetailsChanged;
            monitor.Dispose();
        }

        private async void Monitor_DetailsChanged(PalDetailsRegions regions)
        {
            // Windows.Media.Ocr.OcrEngine permits only one RecognizeAsync call at a
            // time. Screen changes can arrive while the previous frame is still being
            // processed, so discard overlapping frames and process the next change.
            if (Interlocked.Exchange(ref recognitionInProgress, 1) == 1)
                return;

            try
            {
                var observation = await recognizer.RecognizeAsync(regions);
                if (observation?.Pal != null)
                    App.Current.Dispatcher.BeginInvoke(() => ObservationEvaluated?.Invoke(observation));

                if (observation?.CanBeAutomaticallyAdded != true)
                {
                    var candidate = observation?.Pal?.LocalizedNames.GetValueOrElse(
                        "ko",
                        observation.Pal.Name
                    ) ?? "이름 미확인";
                    StatusChanged?.Invoke(
                        $"인식 후보: {candidate} ({observation?.Confidence ?? 0:P0})"
                    );
                    return;
                }

                var signature = ObservationSignature(observation);
                var now = DateTime.UtcNow;
                if (recentObservations.TryGetValue(signature, out var previous) && now - previous < DuplicateWindow)
                {
                    StatusChanged?.Invoke("같은 팰의 중복 인식을 건너뜀");
                    return;
                }
                recentObservations[signature] = now;

                foreach (var expired in recentObservations.Where(x => now - x.Value > TimeSpan.FromMinutes(1)).Select(x => x.Key).ToList())
                    recentObservations.Remove(expired);

                App.Current.Dispatcher.Invoke(() =>
                {
                    ProvisionalPals.Add(observation);
                    PalRecognized?.Invoke(observation);
                    StatusChanged?.Invoke($"임시 인식: {observation.Pal.LocalizedNames.GetValueOrElse("ko", observation.Pal.Name)}");
                });
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"화면 인식 오류: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref recognitionInProgress, 0);
            }
        }

        private static string ObservationSignature(LivePalObservation observation) => string.Join(
            "|",
            observation.Pal.InternalName,
            observation.Gender,
            string.Join(",", observation.PassiveSkills.Select(p => p.InternalName).OrderBy(x => x))
        );
    }
}
