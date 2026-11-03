using System;
using System.Collections.Generic;
using System.Linq;

namespace HemoIQ.Models
{
    /// <summary>
    /// Polyspecific, IgG, and C3 direct antiglobulin results for one specimen.
    /// Each value is NT, Negative, W+, 1+–4+, or MF. Evidence only.
    /// </summary>
    public sealed class DatProfile
    {
        public string? Polyspecific { get; init; }
        public string? IgG { get; init; }
        public string? C3 { get; init; }

        public static DatProfile FromPolyspecific(string? dat) => new() { Polyspecific = dat };

        public static bool IsValidGrade(string? value) =>
            string.IsNullOrWhiteSpace(value) ||
            AntigenConstants.DatResults.Any(v => string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase));

        private IEnumerable<(string Name, string Value)> Recorded()
        {
            if (IsRecorded(Polyspecific)) yield return ("polyspecific", Polyspecific!.Trim());
            if (IsRecorded(IgG)) yield return ("IgG", IgG!.Trim());
            if (IsRecorded(C3)) yield return ("C3", C3!.Trim());
        }

        private static bool IsRecorded(string? v) =>
            !string.IsNullOrWhiteSpace(v) && !v.Trim().Equals("NT", StringComparison.OrdinalIgnoreCase);

        private static bool IsNeg(string v) =>
            v.Equals("Negative", StringComparison.OrdinalIgnoreCase) || ReactionGrade.IsNegative(v);

        public bool AnyRecorded => Recorded().Any();
        public bool IsPositive => Recorded().Any(r => !IsNeg(r.Value) && ReactionGrade.IsPositive(r.Value));
        public bool IsNegative => AnyRecorded && Recorded().All(r => IsNeg(r.Value));

        public bool IgGPositive => IsRecorded(IgG) && !IsNeg(IgG!.Trim());
        public bool C3Positive => IsRecorded(C3) && !IsNeg(C3!.Trim());
        /// <summary>C3 reactive with IgG recorded negative.</summary>
        public bool C3Only => C3Positive && IsRecorded(IgG) && IsNeg(IgG!.Trim());

        /// <summary>Names of results graded mixed field, e.g. "IgG".</summary>
        public List<string> MixedFieldResults =>
            Recorded().Where(r => ReactionGrade.Normalize(r.Value) == ReactionGrade.MixedField)
                .Select(r => r.Name).ToList();

        /// <summary>True when IgG or C3 was recorded (not just polyspecific).</summary>
        public bool HasMonospecific => IsRecorded(IgG) || IsRecorded(C3);

        /// <summary>
        /// "2+" when only polyspecific is recorded; otherwise
        /// "polyspecific 2+, IgG 2+, C3 Negative". "NT" when nothing is recorded.
        /// </summary>
        public string Describe()
        {
            var recorded = Recorded().ToList();
            if (recorded.Count == 0) return "NT";
            if (!HasMonospecific) return recorded[0].Value;
            return string.Join(", ", recorded.Select(r => $"{r.Name} {r.Value}"));
        }
    }
}
