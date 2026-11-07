using System;

namespace HemoIQ.Models
{
    public class LabSettings
    {
        public string LabName { get; set; } = "Immunohematology Laboratory";
        public string Department { get; set; } = "";
        public double ProbabilityThreshold { get; set; } = 0.5;
        public int IdentificationCellCount { get; set; } = 3;
        public int AcsRuleoutCount { get; set; } = 3;
        /// <summary>
        /// Suggested multiple-antibody criterion: reactive cells each called antibody needs that are
        /// negative for every other called antigen. 0 turns the check off.
        /// </summary>
        public int SeparationCellCount { get; set; } = 1;
        /// <summary>
        /// Default number of qualifying antigen-positive nonreactive cells
        /// required to rule out an antibody when no per-antibody rule exists.
        /// 1 preserves historical behavior.
        /// </summary>
        public int DefaultMinRuleoutCount { get; set; } = 1;
        public string DefaultSpecimenType { get; set; } = "serum";
        public bool ShowInactiveByDefault { get; set; }
        public bool HideRuledOutAntigenColumns { get; set; }
        /// <summary>
        /// When true, the reaction antigram groups columns by blood-group system
        /// and marks homozygous antigen-positive cells as ++. Display only.
        /// </summary>
        public bool ShowAntigramDosage { get; set; }
        public string DefaultIdentifiedBy { get; set; } = "";
        public int ExpirationWarningDays { get; set; } = 14;
        public int DefaultSpecimenDatingDays { get; set; } = 3;
        /// <summary>
        /// A transfusion this many days or fewer before the specimen makes
        /// serologic patient typing uninterpretable (possible donor cells).
        /// </summary>
        public int RecentTransfusionWindowDays { get; set; } = 90;
        /// <summary>
        /// When true, a confirmed identification counts as Completed only
        /// after a second person records a supervisor review.
        /// </summary>
        public bool RequireSupervisorReview { get; set; }
        /// <summary>Open investigations older than this many days are listed as overdue on Analytics.</summary>
        public int TurnaroundThresholdDays { get; set; } = 3;
        /// <summary>
        /// A transfusion this many days or fewer before collection limits specimen dating
        /// to collection + <see cref="DefaultSpecimenDatingDays"/>.
        /// </summary>
        public int DatingTransfusionLookbackDays { get; set; } = 90;
        /// <summary>Anti-D within this many days of RhIG is flagged as possibly passive.</summary>
        public int RhigPassiveWindowDays { get; set; } = 84;
        public int MaxDatabaseSizeMb { get; set; } = 500;
        public bool WorklistShowIncomplete { get; set; } = true;
        public bool WorklistShowStale { get; set; } = true;
        public bool WorklistShowExpiring { get; set; } = true;
        public bool WorklistShowExpired { get; set; } = true;
        public bool IntendedUseAcknowledged { get; set; }
        /// <summary>
        /// Comma-separated extra reaction phases (e.g. RT, PEG, Gel, Solid,
        /// Dil1–Dil128). Empty keeps the classic IS / 37°C / AHG / CC grid.
        /// Gel, Solid, and PEG are IAT-like evidence. Dilution columns are
        /// titer endpoints only, not a diagnosis.
        /// </summary>
        public string ExtraPhases { get; set; } = "";

        public string IdentificationRuleLabel =>
            $"{IdentificationCellCount} + {IdentificationCellCount}";

        public static LabSettings CreateDefault() => new();

        public void Clamp()
        {
            if (ProbabilityThreshold < 0.3) ProbabilityThreshold = 0.3;
            if (ProbabilityThreshold > 0.95) ProbabilityThreshold = 0.95;
            if (IdentificationCellCount < 1 || IdentificationCellCount > 3)
                IdentificationCellCount = 3;
            if (AcsRuleoutCount < 1 || AcsRuleoutCount > 5)
                AcsRuleoutCount = 3;
            if (SeparationCellCount < 0 || SeparationCellCount > 5)
                SeparationCellCount = 1;
            if (DefaultMinRuleoutCount < 1 || DefaultMinRuleoutCount > 5)
                DefaultMinRuleoutCount = 1;
            if (ExpirationWarningDays < 1) ExpirationWarningDays = 1;
            if (ExpirationWarningDays > 90) ExpirationWarningDays = 90;
            if (DefaultSpecimenDatingDays < 0) DefaultSpecimenDatingDays = 0;
            if (DefaultSpecimenDatingDays > 14) DefaultSpecimenDatingDays = 14;
            if (RhigPassiveWindowDays < 1) RhigPassiveWindowDays = 1;
            if (RhigPassiveWindowDays > 365) RhigPassiveWindowDays = 365;
            if (DatingTransfusionLookbackDays < 1) DatingTransfusionLookbackDays = 1;
            if (DatingTransfusionLookbackDays > 365) DatingTransfusionLookbackDays = 365;
            if (TurnaroundThresholdDays < 1) TurnaroundThresholdDays = 1;
            if (TurnaroundThresholdDays > 60) TurnaroundThresholdDays = 60;
            if (RecentTransfusionWindowDays < 1) RecentTransfusionWindowDays = 1;
            if (RecentTransfusionWindowDays > 365) RecentTransfusionWindowDays = 365;
            if (MaxDatabaseSizeMb < 50) MaxDatabaseSizeMb = 50;
            if (MaxDatabaseSizeMb > 10240) MaxDatabaseSizeMb = 10240;
            if (string.IsNullOrWhiteSpace(LabName))
                LabName = "Immunohematology Laboratory";
            if (string.IsNullOrWhiteSpace(DefaultSpecimenType) ||
                Array.IndexOf(new[] { "serum", "plasma", "eluate" }, DefaultSpecimenType) < 0)
                DefaultSpecimenType = "serum";
            DefaultIdentifiedBy = NormalizeInitials(DefaultIdentifiedBy);
            ExtraPhases = ExtraPhaseParser.NormalizeList(ExtraPhases);
        }

        public static string NormalizeInitials(string? initials)
        {
            var t = (initials ?? string.Empty).Trim();
            return t.Length <= 12 ? t : t[..12];
        }

        public static DateTime? DefaultExpirationDate(DateTime today, int datingDays)
        {
            if (datingDays <= 0) return null;
            return today.Date.AddDays(datingDays);
        }

        public bool ShowsWorklistKind(WorklistKind kind) => kind switch
        {
            WorklistKind.IncompleteReactions => WorklistShowIncomplete,
            WorklistKind.StaleAnalysis => WorklistShowStale,
            WorklistKind.ExpiringSpecimen or WorklistKind.ExpiringPanel => WorklistShowExpiring,
            WorklistKind.ExpiredSpecimen or WorklistKind.ExpiredPanel => WorklistShowExpired,
            _ => true
        };

        public bool IsWorklistCategoryIsolated(string category) => category switch
        {
            "Incomplete" => WorklistShowIncomplete && !WorklistShowStale &&
                            !WorklistShowExpiring && !WorklistShowExpired,
            "Stale" => WorklistShowStale && !WorklistShowIncomplete &&
                       !WorklistShowExpiring && !WorklistShowExpired,
            "Expiring" => WorklistShowExpiring && !WorklistShowIncomplete &&
                          !WorklistShowStale && !WorklistShowExpired,
            "Expired" => WorklistShowExpired && !WorklistShowIncomplete &&
                         !WorklistShowStale && !WorklistShowExpiring,
            _ => false
        };

        public object ClinicalSnapshot() => new
        {
            ProbabilityThreshold,
            IdentificationCellCount,
            AcsRuleoutCount,
            DefaultMinRuleoutCount,
            SeparationCellCount,
            ExtraPhases,
            RecentTransfusionWindowDays
        };

        public string IsolatedWorklistLabel =>
            IsWorklistCategoryIsolated("Incomplete") ? "Showing only incomplete work." :
            IsWorklistCategoryIsolated("Stale") ? "Showing only stale analyses." :
            IsWorklistCategoryIsolated("Expiring") ? "Showing only items nearing expiration." :
            IsWorklistCategoryIsolated("Expired") ? "Showing only expired specimens and panels." :
            "";
    }
}
