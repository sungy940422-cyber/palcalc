using System.Collections.Generic;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class ScreenRecognitionProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int ClientWidth { get; set; }
        public int ClientHeight { get; set; }
        public string Language { get; set; }
        public string WindowMode { get; set; }
        public PalboxProfile Palbox { get; set; }
        public DetailsProfile Details { get; set; }
    }

    public sealed class PalboxProfile
    {
        public GridProfile BoxGrid { get; set; }
        public GridProfile BaseGrid { get; set; }
    }

    public sealed class GridProfile
    {
        public double FirstCenterX { get; set; }
        public double FirstCenterY { get; set; }
        public double ColumnSpacing { get; set; }
        public double RowSpacing { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int IconRadius { get; set; }
    }

    public sealed class DetailsProfile
    {
        public ScreenRegion Name { get; set; }
        public ScreenRegion Gender { get; set; }
        public List<ScreenRegion> Passives { get; set; } = [];
    }

    public sealed class ScreenRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
