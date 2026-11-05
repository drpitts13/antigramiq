using System.Collections.Generic;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// One line naming the clinical context and serology recorded on a specimen,
    /// shown under the Specimen dialog tabs so values on a tab that is not open are
    /// still visible. Unknown, not-transfused, not-pregnant and not-tested values
    /// are left out.
    /// </summary>
    public static class SpecimenContextSummary
    {
        public const string Nothing = "Context: no transfusion, pregnancy, DAT, typing, or previous antibodies recorded.";

        public static string Describe(Specimen s)
        {
            var parts = new List<string>();
            if (TransfusionStatus.Normalize(s.TransfusionStatus) == TransfusionStatus.Transfused)
                parts.Add(s.TransfusionDisplay);
            var preg = PregnancyStatus.Normalize(s.PregnancyStatus);
            if (preg is PregnancyStatus.Pregnant or PregnancyStatus.Recent)
                parts.Add(PregnancyStatus.Label(preg) +
                          (preg == PregnancyStatus.Recent ? $" (ended {s.PregnancyEndDate ?? "date not recorded"})" : ""));
            if (s.RhigDate != null) parts.Add($"RhIG {s.RhigDate}");
            var dat = s.DatDisplay;
            if (dat != AntigenConstants.AntigenNotTested) parts.Add($"DAT {dat}");
            if (!string.IsNullOrWhiteSpace(s.Phenotype)) parts.Add($"phenotype {s.Phenotype.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.Genotype)) parts.Add($"genotype {s.Genotype.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.PreviousAntibodies)) parts.Add($"previous {s.PreviousAntibodies.Trim()}");
            return parts.Count == 0 ? Nothing : "Context: " + string.Join(" · ", parts);
        }
    }
}
