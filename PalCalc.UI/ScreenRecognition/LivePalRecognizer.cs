using System;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class LivePalRecognizer
    {
        private readonly WindowsKoreanOcr ocr;
        private readonly LivePalObservationFactory factory;
        private readonly SemaphoreSlim recognitionLock = new(1, 1);

        public LivePalRecognizer(WindowsKoreanOcr ocr, LivePalObservationFactory factory)
        {
            this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async Task<LivePalObservation> RecognizeAsync(PalDetailsRegions regions)
        {
            if (!await recognitionLock.WaitAsync(0))
                return null;

            try
            {
                var texts = await ocr.ReadDetailsAsync(regions);
                var gender = GenderColorRecognizer.Recognize(regions.Gender);
                return factory.Create(texts.Name, gender.Gender, gender.Confidence, texts.Passives);
            }
            finally
            {
                recognitionLock.Release();
            }
        }
    }
}
