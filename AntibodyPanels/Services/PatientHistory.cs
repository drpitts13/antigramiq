using System;
using System.Collections.Generic;
using System.Linq;
using AntibodyPanels.Data;
using AntibodyPanels.Models;

namespace AntibodyPanels.Services
{
    /// <summary>An antibody confirmed on another specimen with the same MRN.</summary>
    public sealed class PriorConfirmedAntibody
    {
        public string Antibody { get; init; } = string.Empty;
        public string Accession { get; init; } = string.Empty;
        public string Date { get; init; } = string.Empty;

        public string Explanation =>
            $"{Antibody} was confirmed on accession {Accession} ({Date}) for this MRN. " +
            "Antibodies can fall below detection; treat this as historical evidence, not a current identification.";
    }

    /// <summary>
    /// Collects the same patient's other specimens by MRN. Only confirmed
    /// final calls are carried forward; suspected lists are not.
    /// </summary>
    public static class PatientHistory
    {
        public static string? NormalizeMrn(string? mrn)
        {
            var t = (mrn ?? string.Empty).Trim();
            return t.Length == 0 ? null : t.ToUpperInvariant();
        }

        public static List<Specimen> OtherSpecimens(DatabaseService db, Specimen current)
        {
            var mrn = NormalizeMrn(current.Mrn);
            if (mrn == null) return new List<Specimen>();
            return db.GetSpecimensByMrn(mrn)
                .Where(s => !string.Equals(s.AccessionNumber, current.AccessionNumber, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.CreatedDate, StringComparer.Ordinal)
                .ToList();
        }

        public static List<PriorConfirmedAntibody> ConfirmedAntibodies(IEnumerable<Specimen> others)
        {
            var list = new List<PriorConfirmedAntibody>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Oldest first so each antibody cites its earliest confirmation.
            foreach (var s in others.Where(o => o.HasFinalCall).OrderBy(o => o.CreatedDate, StringComparer.Ordinal))
            {
                foreach (var ab in FinalAntibodyParser.Split(s.FinalAntibodies))
                {
                    if (!ab.StartsWith("anti-", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!seen.Add(ab)) continue;
                    list.Add(new PriorConfirmedAntibody
                    {
                        Antibody = ab,
                        Accession = s.AccessionNumber,
                        Date = string.IsNullOrEmpty(s.IdentifiedAt) ? s.CreatedDate : s.IdentifiedAt!.Split('T', ' ')[0],
                    });
                }
            }
            return list;
        }

        /// <summary>One line per other specimen, newest first, for the detail pane and reports.</summary>
        public static List<string> Describe(IEnumerable<Specimen> others) =>
            others.Select(s =>
            {
                var final = s.HasFinalCall ? $"confirmed {s.FinalAntibodies}" : "no confirmed identification";
                return $"{s.AccessionNumber} ({s.CreatedDate}): {final}; DAT {s.DatDisplay}; {s.TransfusionDisplay}";
            }).ToList();
    }
}
