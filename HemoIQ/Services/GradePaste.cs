using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Parses reaction grades pasted from a spreadsheet (one line per cell, tab-separated
    /// columns per phase) into canonical grades. Validation is strict: a value that is not a
    /// recognized grade rejects the whole paste, because an unrecognized grade would otherwise
    /// read as reactive. A blank value is NT. Nothing is applied here.
    /// </summary>
    public static class GradePaste
    {
        /// <summary>The canonical grade for a pasted value, or null when it is not a recognized grade.</summary>
        public static string? Strict(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ReactionGrade.NotTested;
            var normalized = ReactionGrade.Normalize(raw);
            return ReactionGrade.All.Contains(normalized) ? normalized : null;
        }

        /// <summary>Rows of pasted grades, one list per line, each <see cref="Columns"/> wide.</summary>
        public sealed record BlockResult(IReadOnlyList<IReadOnlyList<string>> Rows, int Columns, IReadOnlyList<string> Errors)
        {
            public bool Ok => Errors.Count == 0 && Rows.Count > 0;
        }

        /// <summary>
        /// Parses a tab-separated block copied from a spreadsheet: one line per cell, one column
        /// per phase. A single column is a block one wide. Short lines are padded with NT (a
        /// spreadsheet may drop trailing blanks). Errors are reported by line (and column when
        /// more than one); nothing is returned unless the whole block is valid.
        /// </summary>
        public static BlockResult ParseBlock(string? text, int availableCells, int availablePhases)
        {
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0)
                return new BlockResult(Array.Empty<IReadOnlyList<string>>(), 0, new[] { "The clipboard has no grades to paste." });

            var fields = lines.Select(l => l.Split('\t')).ToList();
            var columns = fields.Max(f => f.Length);
            var errors = new List<string>();
            var rows = new List<IReadOnlyList<string>>();
            for (int i = 0; i < fields.Count; i++)
            {
                var row = new List<string>();
                for (int c = 0; c < columns; c++)
                {
                    var raw = c < fields[i].Length ? fields[i][c] : string.Empty;
                    var g = Strict(raw);
                    if (g == null)
                        errors.Add(columns == 1
                            ? $"Line {i + 1}: \"{raw.Trim()}\" is not a recognized grade."
                            : $"Line {i + 1}, column {c + 1}: \"{raw.Trim()}\" is not a recognized grade.");
                    row.Add(g ?? ReactionGrade.NotTested);
                }
                rows.Add(row);
            }
            if (lines.Count > availableCells)
                errors.Add($"{lines.Count} rows were copied but only {availableCells} cell(s) remain from the selected cell down.");
            if (columns > availablePhases)
                errors.Add($"{columns} columns were copied but only {availablePhases} phase(s) remain from the selected phase to the right.");
            return errors.Count == 0
                ? new BlockResult(rows, columns, errors)
                : new BlockResult(Array.Empty<IReadOnlyList<string>>(), columns, errors);
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

        public static string ErrorMessage(BlockResult r) =>
            "Nothing was pasted.\n\n" + string.Join("\n", r.Errors.Take(8)) +
            (r.Errors.Count > 8 ? $"\n… and {r.Errors.Count - 8} more" : "") +
            "\n\nRecognized grades: " + string.Join(", ", ReactionGrade.All) + " (blank = NT).";
    }
}
