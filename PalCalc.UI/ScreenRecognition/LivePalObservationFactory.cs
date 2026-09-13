using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class LivePalObservationFactory
    {
        private readonly KoreanGameTextMatcher matcher;

        public LivePalObservationFactory(KoreanGameTextMatcher matcher)
        {
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

        public LivePalObservation Create(
            OcrTextResult palName,
            PalGender? gender,
            double genderConfidence,
            IEnumerable<OcrTextResult> passiveTexts)
        {
            var palMatch = matcher.MatchPal(palName?.Text);
            var passiveMatches = (passiveTexts ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x?.Text))
                .Select(x => (Ocr: x, Match: matcher.MatchPassive(x.Text)))
                .Where(x => x.Match.Value != null && x.Match.Confidence >= 0.72)
                .GroupBy(x => x.Match.Value)
                .Select(g => g.OrderByDescending(x => x.Match.Confidence * x.Ocr.Confidence).First())
                .Take(4)
                .ToList();

            var scores = new List<double>
            {
                (palName?.Confidence ?? 0) * palMatch.Confidence,
                gender.HasValue ? genderConfidence : 0
            };
            scores.AddRange(passiveMatches.Select(x => x.Ocr.Confidence * x.Match.Confidence));

            return new LivePalObservation
            {
                Pal = palMatch.Value,
                Gender = gender,
                PassiveSkills = passiveMatches.Select(x => x.Match.Value).ToList(),
                Confidence = scores.Count == 0 ? 0 : scores.Average(),
                CapturedAtUtc = DateTime.UtcNow,
                IsProvisional = true
            };
        }
    }
}
