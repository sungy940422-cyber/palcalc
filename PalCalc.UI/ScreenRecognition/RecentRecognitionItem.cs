using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Linq;
using System.Windows.Media;

namespace PalCalc.UI.ScreenRecognition
{
    public partial class RecentRecognitionItem : ObservableObject
    {
        private readonly LivePalObservation observation;

        public RecentRecognitionItem(LivePalObservation observation)
        {
            this.observation = observation ?? throw new ArgumentNullException(nameof(observation));
            status = observation.CanBeAutomaticallyAdded ? "임시 인식" : "확인 필요";
        }

        public ImageSource Icon => PalViewModel.Make(observation.Pal).Icon;
        public string Name => observation.Pal.LocalizedNames.GetValueOrElse("ko", observation.Pal.Name);
        public string Gender => observation.Gender == PalGender.MALE ? "수컷" :
            observation.Gender == PalGender.FEMALE ? "암컷" : "미확인";
        public string Passives => observation.PassiveSkills.Count == 0
            ? "패시브 없음"
            : string.Join(" · ", observation.PassiveSkills.Select(
                x => x.LocalizedNames.GetValueOrElse("ko", x.Name)
            ));
        public string Confidence => $"{observation.Confidence:P0}";
        public string CapturedAt => observation.CapturedAtUtc.ToLocalTime().ToString("HH:mm:ss");

        [ObservableProperty]
        private string status = "임시 인식";

        public bool Matches(PalInstance pal)
        {
            if (pal == null || pal.Pal != observation.Pal || pal.Gender != observation.Gender)
                return false;

            return observation.PassiveSkills.All((pal.PassiveSkills ?? []).Contains);
        }
    }
}
