using System;
using System.Collections.Generic;

namespace AntibodyPanels.Models
{
    /// <summary>
    /// Structured transfusion history recorded on a specimen. Stored as the
    /// lower-case code; the label is display only.
    /// </summary>
    public static class TransfusionStatus
    {
        public const string Unknown = "unknown";
        public const string NotTransfused = "none";
        public const string Transfused = "transfused";

        public static readonly IReadOnlyList<(string Code, string Label)> Options = new[]
        {
            (Unknown, "Unknown"),
            (NotTransfused, "Not transfused"),
            (Transfused, "Transfused"),
        };

        public static string Normalize(string? code)
        {
            var c = (code ?? string.Empty).Trim().ToLowerInvariant();
            return c is NotTransfused or Transfused ? c : Unknown;
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
