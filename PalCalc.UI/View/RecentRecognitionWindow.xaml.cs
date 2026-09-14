using AdonisUI.Controls;
using PalCalc.UI.ScreenRecognition;
using System.Collections.ObjectModel;

namespace PalCalc.UI.View
{
    public partial class RecentRecognitionWindow : AdonisWindow
    {
        public RecentRecognitionWindow(ObservableCollection<RecentRecognitionItem> entries)
        {
            InitializeComponent();
            DataContext = entries;
        }
    }
}
