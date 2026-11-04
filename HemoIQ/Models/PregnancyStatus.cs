using System.Collections.Generic;

namespace HemoIQ.Models
{
    /// <summary>
    /// Structured pregnancy context recorded on a specimen. Stored as the
    /// lower-case code; the label is display only.
    /// </summary>
    public static class PregnancyStatus
    {
        public const string Unknown = "unknown";
        public const string NotPregnant = "none";
        public const string Pregnant = "pregnant";
        /// <summary>Pregnancy ended (delivery or loss) on the recorded end date.</summary>
        public const string Recent = "recent";

        public static readonly IReadOnlyList<(string Code, string Label)> Options = new[]
        {
            (Unknown, "Unknown"),
            (NotPregnant, "Not pregnant"),
            (Pregnant, "Pregnant"),
            (Recent, "Recently pregnant"),
        };

        public static string Normalize(string? code)
        {
            var c = (code ?? string.Empty).Trim().ToLowerInvariant();
            return c is NotPregnant or Pregnant or Recent ? c : Unknown;
        }

        public static string Label(string? code)
        {
            var c = Normalize(code);
            foreach (var (k, label) in Options)
                if (k == c) return label;
            return "Unknown";
        }
    }
}
