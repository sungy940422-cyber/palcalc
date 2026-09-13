using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class KoreanGameTextMatcher
    {
        private const string Locale = "ko";
        private readonly IReadOnlyList<(Pal Value, string Text)> pals;
        private readonly IReadOnlyList<(PassiveSkill Value, string Text)> passives;

        public KoreanGameTextMatcher(PalDB database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            pals = database.Pals
                .Select(p => (p, Normalize(LocalizedName(p.LocalizedNames, p.Name))))
                .Where(x => x.Item2.Length > 0)
                .ToList();

            passives = database.StandardPassiveSkills
                .Select(p => (p, Normalize(LocalizedName(p.LocalizedNames, p.Name))))
                .Where(x => x.Item2.Length > 0)
                .ToList();
        }

        public (Pal Value, double Confidence) MatchPal(string recognizedText) =>
            BestMatch(recognizedText, pals);

        public (PassiveSkill Value, double Confidence) MatchPassive(string recognizedText) =>
            BestMatch(recognizedText, passives);

        private static string LocalizedName(Dictionary<string, string> names, string fallback) =>
            names != null && names.TryGetValue(Locale, out var localized) ? localized : fallback;

        private static (T Value, double Confidence) BestMatch<T>(string input, IReadOnlyList<(T Value, string Text)> candidates)
        {
            var normalized = Normalize(input);
            if (normalized.Length == 0)
                return (default, 0);

            T bestValue = default;
            var bestScore = 0d;
            foreach (var candidate in candidates)
            {
                var distance = Levenshtein(normalized, candidate.Text);
                var denominator = Math.Max(normalized.Length, candidate.Text.Length);
                var score = denominator == 0 ? 0 : 1d - distance / (double)denominator;

                if (normalized == candidate.Text)
                    score = 1;
                else if (normalized.Contains(candidate.Text, StringComparison.Ordinal) ||
                         candidate.Text.Contains(normalized, StringComparison.Ordinal))
                    score = Math.Max(score, 0.92);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestValue = candidate.Value;
                }
            }

            return (bestValue, bestScore);
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var result = new StringBuilder(value.Length);
            foreach (var c in value.Normalize(NormalizationForm.FormKC))
            {
                if (char.IsLetterOrDigit(c))
                    result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }

        private static int Levenshtein(string left, string right)
        {
            if (left.Length == 0) return right.Length;
            if (right.Length == 0) return left.Length;

            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++) previous[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
                }
                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
