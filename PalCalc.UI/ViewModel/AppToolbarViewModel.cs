using AdonisUI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.CSV;
using PalCalc.UI.Model.Service;
using PalCalc.UI.ScreenRecognition;
using PalCalc.UI.View;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel
{
    internal partial class AppToolbarViewModel : ObservableObject
    {
        private static readonly Uri palCalcDarkColorScheme = new("pack://application:,,,/PalCalc.UI;component/Themes/PalCalcDark.xaml", UriKind.Absolute);
        private static readonly Uri palCalcLightColorScheme = new("pack://application:,,,/PalCalc.UI;component/Themes/PalCalcLight.xaml", UriKind.Absolute);

        private static ILogger logger = Log.ForContext<AppToolbarViewModel>();

        private static AppToolbarViewModel designerInstance;
        public static AppToolbarViewModel DesignerInstance => designerInstance ??= new(Dispatcher.CurrentDispatcher, new AppSettings());

        private readonly Dispatcher dispatcher;
        private readonly AppSettings settings;
        private Uri currentPalCalcColorScheme = palCalcDarkColorScheme;
        private LiveRecognitionController liveRecognition;
        private ProvisionalPalSaveBridge provisionalPalBridge;
        private SaveGameViewModel activeSave;
        private RecentRecognitionWindow recentRecognitionWindow;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LiveRecognitionActionLabel))]
        private bool isLiveRecognitionRunning;

        [ObservableProperty]
        private string liveRecognitionStatus = "중지됨";

        [ObservableProperty]
        private string wishMessage = "어서 와! 오늘은 어떤 팰을 만들어볼까?";

        [ObservableProperty]
        private string wishMoodLabel = "기본";

        private bool isWishAssistantVisible;
        public bool IsWishAssistantVisible
        {
            get => isWishAssistantVisible;
            set
            {
                if (!SetProperty(ref isWishAssistantVisible, value)) return;
                settings.IsWishAssistantVisible = value;
                Storage.SaveAppSettings(settings);
            }
        }

        public string LiveRecognitionActionLabel => IsLiveRecognitionRunning ? "자동 인식 중지" : "자동 인식 시작";
        public ObservableCollection<RecentRecognitionItem> RecentRecognitions { get; } = [];

        public void SetActiveSave(SaveGameViewModel save)
        {
            activeSave = save;
            if (provisionalPalBridge != null)
                provisionalPalBridge.ActiveSave = save;

            if (save == null)
                ShowWish("사용할 세이브를 골라줘. 내가 보유 팰을 정리해줄게!", "궁금");
            else
                ShowWish("세이브를 불러왔어! 목표 팰과 패시브를 골라보자.", "신남");
        }

        public AppToolbarViewModel(Dispatcher dispatcher, AppSettings settings)
        {
            this.dispatcher = dispatcher;
            this.settings = settings;
            isWishAssistantVisible = settings.IsWishAssistantVisible;
            WishAssistantService.NoticePublished += notice =>
            {
                if (dispatcher.CheckAccess()) ShowWish(notice.Message, notice.Mood);
                else dispatcher.BeginInvoke(() => ShowWish(notice.Message, notice.Mood));
            };
            ApplyTheme(settings.IsDarkTheme, saveSettings: false);
        }

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

        public bool IsDebugLoggingEnabled
        {
            get => PCDebug.FileLogLevel.MinimumLevel == Serilog.Events.LogEventLevel.Debug;
            set => PCDebug.FileLogLevel.MinimumLevel = value ? Serilog.Events.LogEventLevel.Debug : PCDebug.DefaultFileLogLevel;
        }

        public bool IsDarkTheme => settings.IsDarkTheme;
        public bool IsLightTheme => !settings.IsDarkTheme;

        [RelayCommand]
        private void ToggleLiveRecognition()
        {
            try
            {
                if (liveRecognition == null)
                {
                    liveRecognition = new LiveRecognitionController(PalDB.LoadEmbedded());
                    provisionalPalBridge = new ProvisionalPalSaveBridge(liveRecognition)
                    {
                        ActiveSave = activeSave
                    };
                    provisionalPalBridge.StatusChanged += status => SetLiveRecognitionStatus(status);
                    provisionalPalBridge.PalConfirmed += confirmed =>
                    {
                        var entry = RecentRecognitions.FirstOrDefault(x => x.Matches(confirmed));
                        if (entry != null)
                            entry.Status = "세이브 확인 완료";
                    };
                    liveRecognition.StatusChanged += status =>
                        SetLiveRecognitionStatus(status);
                    liveRecognition.RecognitionAttemptStarted += preview =>
                    {
                        RecentRecognitions.Insert(0, new RecentRecognitionItem(preview));
                        while (RecentRecognitions.Count > 10)
                            RecentRecognitions.RemoveAt(RecentRecognitions.Count - 1);
                    };
                    liveRecognition.ObservationEvaluated += observation =>
                    {
                        var completed = new RecentRecognitionItem(observation);
                        if (RecentRecognitions.FirstOrDefault()?.IsPending == true)
                            RecentRecognitions[0] = completed;
                        else
                            RecentRecognitions.Insert(0, completed);
                        while (RecentRecognitions.Count > 10)
                            RecentRecognitions.RemoveAt(RecentRecognitions.Count - 1);
                    };
                }

                if (liveRecognition.IsRunning)
                {
                    liveRecognition.Stop();
                    ShowWish("자동 인식을 잠시 멈췄어.", "기본");
                }
                else
                {
                    liveRecognition.Start();
                    ShowWish("팰 상세 화면을 열어줘. 내가 이름과 패시브를 읽어볼게!", "궁금");
                }

                IsLiveRecognitionRunning = liveRecognition.IsRunning;
                if (!IsLiveRecognitionRunning)
                    LiveRecognitionStatus = "중지됨";
            }
            catch (Exception ex)
            {
                IsLiveRecognitionRunning = false;
                LiveRecognitionStatus = ex.Message;
                ShowWish("화면 인식을 시작하지 못했어. 설정과 팰 상세 화면을 확인해줘.", "당황");
                AdonisMessageBox.Show(App.Current.MainWindow, ex.Message, "화면 자동 인식");
            }
        }

        [RelayCommand]
        private void HideWishAssistant() => IsWishAssistantVisible = false;

        [RelayCommand]
        private void ResetWishMessage()
        {
            IsWishAssistantVisible = true;
            ShowWish("언제나 네 소원이 여기 있어! 필요한 교배 경로를 같이 찾아보자.", "응원");
        }

        [RelayCommand]
        private void OpenRecentRecognitions()
        {
            if (recentRecognitionWindow?.IsVisible == true)
            {
                recentRecognitionWindow.Activate();
                return;
            }

            recentRecognitionWindow = new RecentRecognitionWindow(RecentRecognitions)
            {
                Owner = App.Current.MainWindow
            };
            recentRecognitionWindow.Closed += (_, _) => recentRecognitionWindow = null;
            recentRecognitionWindow.Show();
        }

        private void SetLiveRecognitionStatus(string status)
        {
            void Apply()
            {
                LiveRecognitionStatus = status;
                UpdateWishForStatus(status);
            }

            if (dispatcher.CheckAccess()) Apply();
            else dispatcher.BeginInvoke(Apply);
        }

        private void UpdateWishForStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return;

            if (status.Contains("오류", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("실패", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("찾지 못", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("읽을 수 없", StringComparison.OrdinalIgnoreCase))
            {
                ShowWish("인식이 잘 안 됐어. 팰 상세 화면을 완전히 띄우고 다시 보여줘!", "당황");
                return;
            }

            if (status.Contains("세이브에서 확인", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("확인 완료", StringComparison.OrdinalIgnoreCase))
            {
                ShowWish($"좋아! {status}", "성공");
                return;
            }

            if (status.Contains("임시 인식", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("임시 목록에 추가", StringComparison.OrdinalIgnoreCase))
            {
                ShowWish($"찾았어! {status.Replace("임시 인식:", "").Trim()}", "신남");
                return;
            }

            if (status.Contains("중복", StringComparison.OrdinalIgnoreCase))
            {
                ShowWish("같은 팰은 한 번만 정리해둘게.", "기본");
                return;
            }

            if (status.Contains("찾는 중", StringComparison.OrdinalIgnoreCase))
                ShowWish("팰월드 창을 찾고 있어. 잠깐만 기다려줘!", "궁금");
        }

        private void ShowWish(string message, string mood)
        {
            WishMessage = message;
            WishMoodLabel = mood;
        }

        [RelayCommand]
        private void ExportCrashLog()
        {
            var sfd = new SaveFileDialog();
            sfd.FileName = "CRASHLOG.zip";
            sfd.Filter = "ZIP | *.zip";
            sfd.AddExtension = true;
            sfd.DefaultExt = "zip";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    CrashSupport.PrepareSupportFile(sfd.FileName);
                }
                catch (Exception e)
                {
                    logger.Warning(e, "unexpected error when attempting to create crashlog file");
                    AdonisMessageBox.Show(LocalizationCodes.LC_CRASHLOG_FAILED.Bind().Value, caption: "");
                }
            }
        }

        [RelayCommand]
        private void OpenAboutWindow()
        {
            var window = new AboutWindow();
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
        }

        [RelayCommand]
        private void ResetUiLayout()
        {
            UILayoutStore.Reset();
            AdonisMessageBox.Show(
                App.Current.MainWindow,
                LocalizationCodes.LC_UI_LAYOUT_RESET_NOTICE.Bind().Value
            );
        }

        [RelayCommand]
        private void ForceCheckForUpdates()
        {
            Task.Run(async () =>
            {
                var result = await AppUpdates.CheckForUpdates();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // don't need to run this synchronously
                dispatcher.BeginInvoke(() =>
                {
                    if (result.Status == AppUpdateCheckStatus.Failed)
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_UPDATES_CHECK_RESULT_FAILED.Bind().Value);
                        return;
                    }

                    if (result.Status == AppUpdateCheckStatus.UpToDate)
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_UPDATES_CHECK_RESULT_ON_LATEST.Bind().Value);
                        return;
                    }

                    AppUpdates.PromptUpdateDownload(result.Version);
                }, DispatcherPriority.ContextIdle);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            });
        }

        [RelayCommand]
        private void UseDarkTheme()
        {
            ApplyTheme(isDarkTheme: true, saveSettings: true);
        }

        [RelayCommand]
        private void UseLightTheme()
        {
            ApplyTheme(isDarkTheme: false, saveSettings: true);
        }

        private void ApplyTheme(bool isDarkTheme, bool saveSettings)
        {
            SetTheme(
                isDarkTheme ? ResourceLocator.DarkColorScheme : ResourceLocator.LightColorScheme,
                isDarkTheme ? palCalcDarkColorScheme : palCalcLightColorScheme
            );

            settings.IsDarkTheme = isDarkTheme;
            if (saveSettings)
                Storage.SaveAppSettings(settings);

            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(IsLightTheme));
        }

        private void SetTheme(Uri adonisColorScheme, Uri palCalcColorScheme)
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, adonisColorScheme);
            ResourceLocator.SetColorScheme(Application.Current.Resources, palCalcColorScheme, currentPalCalcColorScheme);
            currentPalCalcColorScheme = palCalcColorScheme;
        }
    }
}
