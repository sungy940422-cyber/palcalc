using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.Model;
using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ScreenRecognition
{
    public partial class RecentRecognitionItem : ObservableObject
    {
        private readonly LivePalObservation observation;
        private readonly ImageSource capturePreview;

        public RecentRecognitionItem(LivePalObservation observation)
        {
            this.observation = observation ?? throw new ArgumentNullException(nameof(observation));
            status = observation.CanBeAutomaticallyAdded ? "임시 인식" : "확인 필요";
        }

        public RecentRecognitionItem(BitmapSource capturePreview)
        {
            this.capturePreview = capturePreview ?? throw new ArgumentNullException(nameof(capturePreview));
            status = "OCR 처리 중";
        }

        public bool IsPending => observation == null;
        public ImageSource Icon => capturePreview ?? (observation.Pal == null
            ? PalIcon.DefaultIcon
            : PalViewModel.Make(observation.Pal).Icon);
        public string Name => observation == null ? "캡처 확인" : observation.Pal == null
            ? "이름 미확인"
            : observation.Pal.LocalizedNames.GetValueOrElse("ko", observation.Pal.Name);
        public string RawName => observation == null ? "이름 영역 미리보기" :
            string.IsNullOrWhiteSpace(observation.RawName)
            ? "OCR 원문: (읽지 못함)"
            : $"OCR 원문: {observation.RawName}";
        public string Gender => observation == null ? "" :
            observation.Gender == PalGender.MALE ? "수컷" :
            observation.Gender == PalGender.FEMALE ? "암컷" : "미확인";
        public string Passives => observation == null ? "화면 글자를 읽는 중입니다" :
            observation.PassiveSkills.Count == 0
            ? "패시브 없음"
            : string.Join(" · ", observation.PassiveSkills.Select(
                x => x.LocalizedNames.GetValueOrElse("ko", x.Name)
            ));
        public string Confidence => observation == null ? "" : $"{observation.Confidence:P0}";
        public string CapturedAt => observation?.CapturedAtUtc.ToLocalTime().ToString("HH:mm:ss")
            ?? DateTime.Now.ToString("HH:mm:ss");

        [ObservableProperty]
        private string status = "임시 인식";

        public bool Matches(PalInstance pal)
        {
            if (observation?.Pal == null || pal == null ||
                pal.Pal != observation.Pal || pal.Gender != observation.Gender)
                return false;

            return observation.PassiveSkills.All((pal.PassiveSkills ?? []).Contains);
        }
    }
}
