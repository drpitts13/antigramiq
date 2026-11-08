using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Parses a pasted column of reaction grades (one value per line, as copied from a
    /// spreadsheet column) into canonical grades. Validation is strict: a value that is not a
    /// recognized grade rejects the whole paste, because an unrecognized grade would otherwise
    /// read as reactive. A blank line inside the column is NT. Nothing is applied here.
    /// </summary>
    public static class GradePaste
    {
        public sealed record Result(IReadOnlyList<string> Grades, IReadOnlyList<string> Errors)
        {
            public bool Ok => Errors.Count == 0 && Grades.Count > 0;
        }

        /// <summary>The canonical grade for a pasted value, or null when it is not a recognized grade.</summary>
        public static string? Strict(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ReactionGrade.NotTested;
            var normalized = ReactionGrade.Normalize(raw);
            return ReactionGrade.All.Contains(normalized) ? normalized : null;
        }

        public static Result Parse(string? text, int availableCells)
        {
            var errors = new List<string>();
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0)
                return new Result(Array.Empty<string>(), new[] { "The clipboard has no grades to paste." });
            if (lines.Any(l => l.Contains('\t')))
                return new Result(Array.Empty<string>(),
                    new[] { "Paste one column at a time: the clipboard holds several columns (tab-separated)." });

            var grades = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                var g = Strict(lines[i]);
                if (g == null) errors.Add($"Line {i + 1}: \"{lines[i].Trim()}\" is not a recognized grade.");
                else grades.Add(g);
            }
            if (lines.Count > availableCells)
                errors.Add($"{lines.Count} values were copied but only {availableCells} cell(s) remain from the selected cell down.");
            return new Result(errors.Count == 0 ? grades : Array.Empty<string>(), errors);
        }

        /// <summary>Indexes where a pasted grade would replace a different, already entered grade.</summary>
        public static List<int> Overwrites(IReadOnlyList<string> current, IReadOnlyList<string> pasted)
        {
            var list = new List<int>();
            for (int i = 0; i < pasted.Count && i < current.Count; i++)
            {
                var cur = ReactionGrade.Normalize(current[i]);
                if (cur != ReactionGrade.NotTested && cur != pasted[i]) list.Add(i);
            }
            return list;
        }

        public static string ErrorMessage(Result r) =>
            "Nothing was pasted.\n\n" + string.Join("\n", r.Errors.Take(8)) +
            (r.Errors.Count > 8 ? $"\n… and {r.Errors.Count - 8} more" : "") +
            "\n\nRecognized grades: " + string.Join(", ", ReactionGrade.All) + " (blank = NT).";
    }
}
