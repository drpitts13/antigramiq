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
