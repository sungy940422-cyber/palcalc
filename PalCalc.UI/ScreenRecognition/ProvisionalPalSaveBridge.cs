using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Mapped.Saves;
using PalCalc.UI.ViewModel.PalDerived;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class ProvisionalPalSaveBridge : IDisposable
    {
        public const string ContainerLabel = "화면 자동 인식 (임시)";

        private readonly LiveRecognitionController controller;
        private SaveGameViewModel activeSave;
        private CancellationTokenSource reloadDebounce;

        public ProvisionalPalSaveBridge(LiveRecognitionController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            controller.PalRecognized += Controller_PalRecognized;
            Storage.SaveReloaded += Storage_SaveReloaded;
        }

        public SaveGameViewModel ActiveSave
        {
            get => activeSave;
            set
            {
                if (activeSave?.Value != null)
                    activeSave.Value.Updated -= ActiveSave_Updated;

                reloadDebounce?.Cancel();
                reloadDebounce?.Dispose();
                reloadDebounce = null;
                activeSave = value;

                if (activeSave?.Value != null)
                    activeSave.Value.Updated += ActiveSave_Updated;
            }
        }

        public event Action<string> StatusChanged;

        public void Dispose()
        {
            ActiveSave = null;
            controller.PalRecognized -= Controller_PalRecognized;
            Storage.SaveReloaded -= Storage_SaveReloaded;
        }

        private void ActiveSave_Updated(ISaveGame changedSave)
        {
            var save = ActiveSave;
            if (save?.Value != changedSave || !changedSave.IsLocal) return;

            var next = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref reloadDebounce, next);
            previous?.Cancel();
            previous?.Dispose();
            _ = ReloadAfterSaveSettles(save, next.Token);
        }

        private async Task ReloadAfterSaveSettles(SaveGameViewModel save, CancellationToken token)
        {
            try
            {
                // Palworld writes several files in a burst. Wait for the save to settle before parsing.
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                await Task.Run(() =>
                    Storage.ReloadSave(
                        save.Parent.SourceLocation,
                        save.Value,
                        PalDB.LoadEmbedded(),
                        GameSettingsViewModel.Load(save.Value).ModelObject
                    ), token
                );
            }
            catch (TaskCanceledException) { }
            catch (IOException)
            {
                StatusChanged?.Invoke("저장 중입니다. 다음 변경에서 다시 확인합니다");
            }
            catch (UnauthorizedAccessException)
            {
                StatusChanged?.Invoke("세이브 파일을 읽을 수 없습니다");
            }
        }

        private void Controller_PalRecognized(LivePalObservation observation)
        {
            var save = ActiveSave;
            if (save == null)
            {
                StatusChanged?.Invoke("먼저 사용할 세이브를 열어 주세요");
                return;
            }

            RunOnUiThread(() =>
            {
                var customizations = save.Customizations;
                var container = customizations.CustomContainers.FirstOrDefault(c => c.Label == ContainerLabel);
                if (container == null)
                {
                    container = new CustomContainerViewModel(new CustomContainer { Label = ContainerLabel });
                    customizations.CustomContainers.Add(container);
                }

                var instance = ToPalInstance(observation, container.ContainerId);
                container.Contents.Add(new CustomPalInstanceViewModel(instance));
                StatusChanged?.Invoke($"임시 목록에 추가: {KoreanName(observation.Pal)}");
            });
        }

        private void Storage_SaveReloaded(ISaveGame changedSave)
        {
            var save = ActiveSave;
            if (save?.Value != changedSave) return;

            RunOnUiThread(() => Reconcile(save));
        }

        private void Reconcile(SaveGameViewModel save)
        {
            var container = save.Customizations.CustomContainers.FirstOrDefault(c => c.Label == ContainerLabel);
            if (container == null || container.Contents.Count == 0) return;

            var ownedPals = save.CachedValue?.OwnedPals ?? [];
            var confirmed = container.Contents
                .Where(provisional => provisional.ModelObject is PalInstance p && ownedPals.Any(saved => Matches(p, saved)))
                .ToList();

            foreach (var item in confirmed)
                container.Contents.Remove(item);

            if (container.Contents.Count == 0)
                save.Customizations.CustomContainers.Remove(container);

            if (confirmed.Count > 0)
                StatusChanged?.Invoke($"자동 저장에서 {confirmed.Count}마리 확인 완료");
        }

        private static bool Matches(PalInstance provisional, PalInstance saved)
        {
            if (provisional.Pal != saved.Pal || provisional.Gender != saved.Gender)
                return false;

            var expected = provisional.PassiveSkills ?? [];
            var actual = saved.PassiveSkills ?? [];
            return expected.All(actual.Contains);
        }

        private static PalInstance ToPalInstance(LivePalObservation observation, string containerId) => new()
        {
            InstanceId = $"screen-{Guid.NewGuid():N}",
            Level = 1,
            Pal = observation.Pal,
            Gender = observation.Gender.Value,
            PassiveSkills = observation.PassiveSkills.ToList(),
            ActiveSkills = [],
            EquippedActiveSkills = [],
            Location = new PalLocation
            {
                Type = LocationType.Custom,
                ContainerId = containerId
            }
        };

        private static string KoreanName(Pal pal) =>
            pal.LocalizedNames.GetValueOrElse("ko", pal.Name);

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.BeginInvoke(action);
        }
    }
}
