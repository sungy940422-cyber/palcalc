using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class PalDetailsRegions
    {
        public BitmapSource Name { get; init; }
        public BitmapSource Gender { get; init; }
        public IReadOnlyList<BitmapSource> Passives { get; init; } = [];
    }

    public static class ScreenRegionExtractor
    {
        public static PalDetailsRegions ExtractDetails(BitmapSource frame, ScreenRecognitionProfile profile)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var scaleX = frame.PixelWidth / (double)profile.ClientWidth;
            var scaleY = frame.PixelHeight / (double)profile.ClientHeight;

            var passives = new List<BitmapSource>();
            foreach (var passive in profile.Details.Passives)
                passives.Add(Crop(frame, passive, scaleX, scaleY));

            return new PalDetailsRegions
            {
                Name = Crop(frame, profile.Details.Name, scaleX, scaleY),
                Gender = Crop(frame, profile.Details.Gender, scaleX, scaleY),
                Passives = passives
            };
        }

        private static BitmapSource Crop(BitmapSource source, ScreenRegion region, double scaleX, double scaleY)
        {
            var x = Math.Clamp((int)Math.Round(region.X * scaleX), 0, source.PixelWidth - 1);
            var y = Math.Clamp((int)Math.Round(region.Y * scaleY), 0, source.PixelHeight - 1);
            var width = Math.Clamp((int)Math.Round(region.Width * scaleX), 1, source.PixelWidth - x);
            var height = Math.Clamp((int)Math.Round(region.Height * scaleY), 1, source.PixelHeight - y);

            var result = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
            result.Freeze();
            return result;
        }
    }
}
