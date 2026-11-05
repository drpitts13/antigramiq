using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// The structured antigen-negative unit requirement on a patient record, stored as
    /// canonical antigen codes in panel order ("E, K, Fya"). Entry accepts commas,
    /// semicolons or spaces between antigens, an optional "-", "neg" or "negative"
    /// suffix, and paired notation such as Fy(a-). Case decides C/c, E/e, K/k, S/s;
    /// other antigens are matched case-insensitively when only one fits.
    /// </summary>
    public static class AntigenNegativeRequirement
    {
        public sealed class ParseResult
        {
            public List<string> Antigens { get; } = new();
            public List<string> Unrecognized { get; } = new();
            public string? Canonical => Antigens.Count == 0 ? null : string.Join(", ", Antigens);
        }

        public static ParseResult Parse(string? text)
        {
            var result = new ParseResult();
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Fy(a-) / Jk(b−) → Fya / Jkb before splitting.
            var expanded = Regex.Replace(text, @"\b(Fy|Jk|Kp|Js|Lu|Le)\s*\(\s*([ab])\s*[-−–]?\s*\)", "$1$2", RegexOptions.IgnoreCase);
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in expanded.Split(new[] { ',', ';', ' ', '\t', '\r', '\n', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = Regex.Replace(raw.Trim(), @"(?i)(?:[-−–]|\(\s*[-−–]\s*\))?(?:neg(?:ative)?)?$", "").Trim('-', '−', '–', ' ');
                if (token.Length == 0) continue;
                if (Resolve(token) is string antigen) found.Add(antigen);
                else if (!result.Unrecognized.Contains(raw.Trim())) result.Unrecognized.Add(raw.Trim());
            }
            result.Antigens.AddRange(AntigenConstants.AllKnownAntigens.Where(found.Contains));
            return result;
        }

        /// <summary>Canonical antigens from a stored value; empty when none.</summary>
        public static IReadOnlyList<string> Antigens(string? stored) => Parse(stored).Antigens;

        /// <summary>Display such as "E−, K−, Fy(a−)" for reports and the detail pane.</summary>
        public static string Display(string? stored)
        {
            var list = Antigens(stored);
            if (list.Count == 0) return "None recorded";
            return string.Join(", ", list.Select(a =>
                Regex.Match(a, "^(Fy|Jk|Kp|Js|Lu|Le)([ab])$") is { Success: true } m
                    ? $"{m.Groups[1].Value}({m.Groups[2].Value}−)"
                    : a + "−"));
        }

        private static string? Resolve(string token)
        {
            if (AntigenConstants.IsKnown(token)) return token;
            var matches = AntigenConstants.AllKnownAntigens
                .Where(a => string.Equals(a, token, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
