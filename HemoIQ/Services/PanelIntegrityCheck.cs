using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Panel data-quality checks for the Worklist, analysis notes, and panel entry.
    /// They flag likely errors for review; they change no data.
    /// </summary>
    public static class PanelIntegrityCheck
    {
        /// <summary>
        /// Non-AC cells with no antigen typed positive. Every real red cell carries c or C,
        /// e or E, and k or K, so a cell with nothing positive almost certainly never had its
        /// typing entered. New panel cells start typed negative, which reads as antigen-negative.
        /// </summary>
        public static List<string> UntypedCells(IEnumerable<PanelCell> cells) =>
            cells.Where(c => !string.Equals(c.CellNumber, "AC", StringComparison.OrdinalIgnoreCase))
                .Where(c => !c.Antigens.Keys.Any(c.IsAntigenPositive))
                .Select(c => c.CellNumber)
                .ToList();

        public static string DescribeUntyped(Panel panel, IReadOnlyList<string> cells, int totalCells) =>
            cells.Count == totalCells
                ? $"Lot {panel.LotNumber ?? "N/A"}: no antigen is typed positive on any of its {totalCells} cells. " +
                  "The antigram has probably not been entered; analysis would read every cell as antigen-negative."
                : $"Lot {panel.LotNumber ?? "N/A"}: cell(s) {string.Join(", ", cells)} have no antigen typed positive. " +
                  "Their typing has probably not been entered; analysis would read them as antigen-negative.";

        public sealed record TypingSummary(int Cells, int FullyTyped, List<string> Partial, List<string> Untyped)
        {
            public bool HasIssues => Partial.Count > 0 || Untyped.Count > 0;

            /// <summary>Short text for the Panels list, e.g. "24 antigens · Fya partial · cells 3, 7 untyped".</summary>
            public string Display
            {
                get
                {
                    if (Cells == 0) return "No cells";
                    if (FullyTyped == 0 && Partial.Count == 0) return "Not typed";
                    var parts = new List<string> { $"{FullyTyped} antigen{(FullyTyped == 1 ? "" : "s")}" };
                    if (Partial.Count > 0)
                        parts.Add($"{string.Join(", ", Partial.Take(3))}{(Partial.Count > 3 ? $" +{Partial.Count - 3}" : "")} partial");
                    if (Untyped.Count > 0)
                        parts.Add($"cell{(Untyped.Count == 1 ? "" : "s")} {string.Join(", ", Untyped)} untyped");
                    return string.Join(" · ", parts);
                }
            }
        }

        /// <summary>
        /// How completely a panel is typed, over non-AC cells: antigens typed on every cell,
        /// antigens typed on some cells but NT on others (likely an entry gap), and cells with
        /// nothing typed positive. Antigens NT on every cell are simply not on the antigram.
        /// </summary>
        public static TypingSummary Summarize(IEnumerable<PanelCell> cells)
        {
            var list = cells.Where(c => !string.Equals(c.CellNumber, "AC", StringComparison.OrdinalIgnoreCase)).ToList();
            var antigens = list.SelectMany(c => c.Antigens.Keys).Distinct(StringComparer.Ordinal).ToList();
            int full = 0;
            var partial = new List<string>();
            foreach (var ag in antigens)
            {
                var typed = list.Count(c => c.HasTypedAntigen(ag));
                if (typed == list.Count) full++;
                else if (typed > 0) partial.Add(ag);
            }
            var ordered = partial
                .OrderBy(a => AntigenConstants.Antigens.ToList().IndexOf(a) is var i && i < 0 ? int.MaxValue : i)
                .ThenBy(a => a, StringComparer.Ordinal)
                .ToList();
            return new TypingSummary(list.Count, full, ordered, UntypedCells(list));
        }

        /// <summary>Normalized lot key: trimmed, case-insensitive. Null when the lot is blank.</summary>
        public static string? LotKey(string? lot)
        {
            var t = (lot ?? string.Empty).Trim();
            return t.Length == 0 ? null : t.ToUpperInvariant();
        }

        /// <summary>
        /// Same vendor lot stored more than once. Lots match ignoring case and spaces at the ends;
        /// vendors match ignoring case, and a blank vendor matches any vendor.
        /// </summary>
        public static bool SameLot(Panel a, string? vendor, string? lot)
        {
            var key = LotKey(lot);
            if (key == null || LotKey(a.LotNumber) != key) return false;
            var va = (a.Vendor ?? string.Empty).Trim();
            var vb = (vendor ?? string.Empty).Trim();
            return va.Length == 0 || vb.Length == 0 || string.Equals(va, vb, StringComparison.OrdinalIgnoreCase);
        }

        public static List<Panel> DuplicatesOf(IEnumerable<Panel> panels, string? vendor, string? lot, int? excludePanelId = null) =>
            panels.Where(p => p.PanelId != excludePanelId && SameLot(p, vendor, lot)).ToList();

        /// <summary>Groups of two or more panels sharing a lot, where at least one is active.</summary>
        public static List<List<Panel>> DuplicateLotGroups(IEnumerable<Panel> panels)
        {
            var list = panels.Where(p => LotKey(p.LotNumber) != null).OrderBy(p => p.PanelId).ToList();
            var groups = new List<List<Panel>>();
            var used = new HashSet<int>();
            foreach (var p in list)
            {
                if (used.Contains(p.PanelId)) continue;
                var group = list.Where(o => !used.Contains(o.PanelId) && SameLot(o, p.Vendor, p.LotNumber)).ToList();
                foreach (var g in group) used.Add(g.PanelId);
                if (group.Count > 1 && group.Any(g => g.IsActive)) groups.Add(group);
            }
            return groups;
        }

        public static string DescribeDuplicates(IReadOnlyList<Panel> group) =>
            string.Join("; ", group.Select(p =>
                $"#{p.PanelId} '{p.Name}'{(string.IsNullOrWhiteSpace(p.Vendor) ? "" : $" ({p.Vendor})")}" +
                $"{(p.IsActive ? ", active" : ", inactive")}"));
    }
}
