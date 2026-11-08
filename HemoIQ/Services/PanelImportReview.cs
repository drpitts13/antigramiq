using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class SchemaDiff
    {
        public List<string> Added { get; } = new();
        public List<string> Removed { get; } = new();
        public bool Changed => Added.Count > 0 || Removed.Count > 0;
    }

    public sealed class PanelImportOutcome
    {
        public int PanelId { get; init; }
        public PanelImportReview Review { get; init; } = new();
    }

    public sealed class PanelImportReview
    {
        public int PanelId { get; set; }
        public string? Sha256 { get; set; }
        public string? ArtifactPath { get; set; }
        public string? PriorLotNumber { get; set; }
        public int? PriorPanelId { get; set; }
        public SchemaDiff Schema { get; set; } = new();
        public List<PanelTypingIssue> TypingIssues { get; set; } = new();
        /// <summary>Typing completeness of the stored lot, e.g. "24 antigens · Fya partial".</summary>
        public string? TypingSummary { get; set; }
        /// <summary>Antigens the prior lot types on every cell but this lot does not, e.g. "Fya (9 of 11 cells)".</summary>
        public List<string> CoverageGaps { get; set; } = new();
        public bool StoredInactive { get; set; } = true;
        public string Explanation { get; set; } = string.Empty;
    }

    public static class PanelImportReviewer
    {
        public static string Hash(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        public static SchemaDiff CompareSchema(IEnumerable<string> incoming, IEnumerable<string> prior)
        {
            var next = Normalize(incoming);
            var prev = Normalize(prior);
            var diff = new SchemaDiff();
            diff.Added.AddRange(next.Where(a => !prev.Contains(a)));
            diff.Removed.AddRange(prev.Where(a => !next.Contains(a)));
            return diff;
        }

        /// <summary>
        /// Fills the typing summary and, when a prior lot exists, the antigens the prior lot typed
        /// on every cell that the new lot types only partly. Uses the stored cells of both lots.
        /// </summary>
        public static void ApplyCoverage(PanelImportReview review, IReadOnlyList<PanelCell> storedCells,
            IReadOnlyList<PanelCell>? priorCells)
        {
            review.TypingSummary = PanelIntegrityCheck.Summarize(storedCells).Display;
            if (priorCells == null || priorCells.Count == 0) return;
            var compare = PanelComparer.Compare("prior", priorCells, Array.Empty<string>(),
                "imported", storedCells, Array.Empty<string>());
            // An antigen absent from every new cell is already reported as a removed column.
            review.CoverageGaps = compare.CoverageGaps.Where(g => !g.Contains("(0 of ", StringComparison.Ordinal)).ToList();
        }

        public static string Explain(PanelImportReview review, string? lotNumber, string? vendor)
        {
            var sb = new StringBuilder();
            var lot = string.IsNullOrWhiteSpace(lotNumber) ? "this lot" : lotNumber.Trim();
            var vendorLabel = string.IsNullOrWhiteSpace(vendor) ? "the vendor" : vendor.Trim();
            sb.Append(lot).Append(" from ").Append(vendorLabel)
                .Append(" was stored inactive pending review.");
            if (!string.IsNullOrWhiteSpace(review.Sha256))
                sb.Append(" Source artifact SHA-256 ").Append(review.Sha256)
                    .Append(" was retained");
            else
                sb.Append(" No source artifact was retained (bytes were not supplied)");
            sb.Append('.');
            if (string.IsNullOrWhiteSpace(review.PriorLotNumber))
            {
                sb.Append(" No prior lot from this vendor/product line was available to compare.");
            }
            else if (!review.Schema.Changed)
            {
                sb.Append(" Antigen schema matches prior lot ").Append(review.PriorLotNumber).Append('.');
            }
            else
            {
                sb.Append(" Antigen schema differs from prior lot ").Append(review.PriorLotNumber).Append('.');
                if (review.Schema.Added.Count > 0)
                    sb.Append(" Added: ").Append(string.Join(", ", review.Schema.Added)).Append('.');
                if (review.Schema.Removed.Count > 0)
                    sb.Append(" Removed: ").Append(string.Join(", ", review.Schema.Removed)).Append('.');
            }
            if (!string.IsNullOrWhiteSpace(review.TypingSummary))
                sb.Append(" Typing: ").Append(review.TypingSummary).Append('.');
            if (review.CoverageGaps.Count > 0)
                sb.Append(" Typed on every cell of prior lot ").Append(review.PriorLotNumber)
                    .Append(" but not of this lot: ").Append(string.Join(", ", review.CoverageGaps))
                    .Append(". Check the vendor sheet for these columns before activating; untyped cells stay NT.");
            AppendTypingIssues(sb, review.TypingIssues);
            sb.Append(" Activate only after a qualified user reviews the antigram. This is not a diagnosis.");
            return sb.ToString();
        }

        private static void AppendTypingIssues(StringBuilder sb, IReadOnlyList<PanelTypingIssue> issues)
        {
            if (issues == null || issues.Count == 0) return;
            sb.Append(" Typing review: ");
            const int cap = 12;
            for (int i = 0; i < issues.Count && i < cap; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(issues[i].Explanation);
            }
            if (issues.Count > cap)
                sb.Append(" Additional typing issues were omitted from this summary.");
        }

        private static List<string> Normalize(IEnumerable<string> antigens) =>
            antigens
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
