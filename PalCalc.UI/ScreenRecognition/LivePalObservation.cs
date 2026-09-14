using PalCalc.Model;
using System;
using System.Collections.Generic;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class LivePalObservation
    {
        public Pal Pal { get; init; }
        public PalGender? Gender { get; init; }
        public IReadOnlyList<PassiveSkill> PassiveSkills { get; init; } = [];
        public string RawName { get; init; } = "";
        public IReadOnlyList<string> RawPassives { get; init; } = [];
        public double Confidence { get; init; }
        public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
        public bool IsProvisional { get; init; } = true;

        public bool CanBeAutomaticallyAdded =>
            Pal != null &&
            Gender.HasValue &&
            Confidence >= 0.90;
    }
}
