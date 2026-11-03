using System;
using System.Collections.Generic;

namespace AntibodyPanels.Models
{
    public class Specimen
    {
        public string AccessionNumber { get; set; } = string.Empty;
        /// <summary>Medical record number; links specimens of the same patient. Stored upper-case.</summary>
        public string? Mrn { get; set; }
        public string Type { get; set; } = "serum";
        public string? ExpirationDate { get; set; }
        public string CreatedDate { get; set; } = string.Empty;
        public string? ReactionsUpdatedAt { get; set; }
        public string? LastAnalyzedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public string? Notes { get; set; }
        public string? Phenotype { get; set; }
        public string? Genotype { get; set; }
        public string? PreviousAntibodies { get; set; }
        /// <summary>Polyspecific DAT (the original single DAT value).</summary>
        public string? DatResult { get; set; }
        public string? DatIgg { get; set; }
        public string? DatC3 { get; set; }
        public DatProfile Dat => new() { Polyspecific = DatResult, IgG = DatIgg, C3 = DatC3 };
        public string DatDisplay => Dat.Describe();
        /// <summary>Normalized <see cref="Models.TransfusionStatus"/> code.</summary>
        public string TransfusionStatus { get; set; } = Models.TransfusionStatus.Unknown;
        public string? LastTransfusionDate { get; set; }
        public string TransfusionDisplay =>
            Models.TransfusionStatus.Normalize(TransfusionStatus) == Models.TransfusionStatus.Transfused
                ? $"Transfused (last {LastTransfusionDate ?? "date not recorded"})"
                : Models.TransfusionStatus.Label(TransfusionStatus);

        public string? FinalAntibodies { get; set; }
        public string? FinalComment { get; set; }
        public string? IdentifiedBy { get; set; }
        public string? IdentifiedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewedAt { get; set; }
        public bool HasFinalCall => !string.IsNullOrWhiteSpace(FinalAntibodies);
        public bool IsReviewed => !string.IsNullOrWhiteSpace(ReviewedBy);
        public string FinalIdDisplay => HasFinalCall ? FinalAntibodies! : "";

        public string ListDisplay => FormatListDisplay(AccessionNumber, Type, FinalIdDisplay);

        public bool IsExpired => ExpirationStatus.IsExpired(ExpirationDate, DateTime.Today);
        public bool IsExpiringSoon =>
            ExpirationStatus.IsExpiringSoon(ExpirationDate, DateTime.Today, ExpirationWarningDays);

        private static int ExpirationWarningDays =>
            Services.AppSettings.Current.ExpirationWarningDays;

        public static string FormatListDisplay(string? accession, string? type, string? finalId)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(accession)) parts.Add(accession.Trim());
            if (!string.IsNullOrWhiteSpace(type)) parts.Add(type.Trim());
            if (!string.IsNullOrWhiteSpace(finalId)) parts.Add(finalId.Trim());
            return string.Join("  ·  ", parts);
        }

        public List<SpecimenAntibody> Antibodies { get; set; } = new();
        public List<SpecimenRuleout> Ruleouts { get; set; } = new();
        public List<Panel> LinkedPanels { get; set; } = new();

        public bool IsAnalysisStale =>
            ReactionsUpdatedAt != null &&
            (LastAnalyzedAt == null || string.Compare(ReactionsUpdatedAt, LastAnalyzedAt) > 0);

        public bool MatchesFilter(string? query) =>
            TextFilter.Matches(query, AccessionNumber, Mrn, Type, Phenotype, Genotype, PreviousAntibodies,
                Notes, FinalAntibodies, DatResult, DatIgg, DatC3, ExpirationDate);
    }
}
