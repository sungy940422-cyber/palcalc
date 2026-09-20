using PalCalc.Model;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ScreenRecognition
{
    public static class GenderColorRecognizer
    {
        public static (PalGender? Gender, double Confidence) Recognize(BitmapSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);

            var male = 0;
            var female = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var blue = pixels[i];
                var green = pixels[i + 1];
                var red = pixels[i + 2];

                if (blue > 145 && blue > red * 1.18 && green > red * 0.85)
                    male++;
                if (red > 155 && red > blue * 1.18 && blue > green * 0.75)
                    female++;
            }

            var total = male + female;
            if (total < 8) return (null, 0);

            var strongest = Math.Max(male, female);
            var confidence = strongest / (double)total;
            if (confidence < 0.68) return (null, confidence);

            return male > female
                ? (PalGender.MALE, confidence)
                : (PalGender.FEMALE, confidence);
        }
    }
}
