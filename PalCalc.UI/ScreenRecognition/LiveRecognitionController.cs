using PalCalc.Model;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class LiveRecognitionController : IDisposable
    {
        private readonly PalworldScreenMonitor monitor;
        private readonly LivePalRecognizer recognizer;
        private readonly Dictionary<string, DateTime> recentObservations = [];
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
            try
            {
                var observation = await recognizer.RecognizeAsync(regions);
                if (observation?.CanBeAutomaticallyAdded != true)
                {
                    StatusChanged?.Invoke("팰 상세정보를 확인하는 중");
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
        }

        private static string ObservationSignature(LivePalObservation observation) => string.Join(
            "|",
            observation.Pal.InternalName,
            observation.Gender,
            string.Join(",", observation.PassiveSkills.Select(p => p.InternalName).OrderBy(x => x))
        );
    }
}
