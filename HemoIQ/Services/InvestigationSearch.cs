using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class InvestigationSearchRow
    {
        public string Accession { get; init; } = string.Empty;
        public string Mrn { get; init; } = string.Empty;
        public string Patient { get; init; } = string.Empty;
        public string DateOfBirth { get; init; } = string.Empty;
        public string Date { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string FinalId { get; init; } = string.Empty;
        public string IdentifiedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Finds investigations by accession, MRN, patient name or DOB, final
    /// antibodies, technologist, previous antibodies, collector, locations, or
    /// ordering provider. Every word of the
    /// query must match at least one field (case-insensitive substring).
    /// </summary>
    public static class InvestigationSearch
    {
        public static List<InvestigationSearchRow> Search(DatabaseService db, string? query,
            DateTime? from = null, DateTime? to = null)
        {
            var words = (query ?? string.Empty)
                .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var patients = db.GetAllPatients().ToDictionary(p => p.Mrn, StringComparer.OrdinalIgnoreCase);
            var fromText = from?.ToString("yyyy-MM-dd");
            var toText = to?.ToString("yyyy-MM-dd");

            var rows = new List<InvestigationSearchRow>();
            foreach (var s in db.GetAllSpecimens())
            {
                var created = s.CreatedDate.Length >= 10 ? s.CreatedDate[..10] : s.CreatedDate;
                if (fromText != null && string.CompareOrdinal(created, fromText) < 0) continue;
                if (toText != null && string.CompareOrdinal(created, toText) > 0) continue;

                Patient? p = null;
                if (s.Mrn != null) patients.TryGetValue(s.Mrn, out p);
                var fields = new[]
                {
                    s.AccessionNumber, s.Mrn, p?.Name, p?.DateOfBirth, s.FinalAntibodies,
                    s.IdentifiedBy, s.PreviousAntibodies, s.FinalComment,
                    s.Collector, s.CollectionLocation, s.OrderingLocation, s.OrderingProvider,
                };
                if (!words.All(w => TextFilter.Matches(w, fields))) continue;

                rows.Add(new InvestigationSearchRow
                {
                    Accession = s.AccessionNumber,
                    Mrn = s.Mrn ?? string.Empty,
                    Patient = p?.Name ?? string.Empty,
                    DateOfBirth = p?.DateOfBirth ?? string.Empty,
                    Date = created,
                    Status = InvestigationStatusService.Compute(db, s).Label,
                    FinalId = s.FinalAntibodies ?? string.Empty,
                    IdentifiedBy = s.IdentifiedBy ?? string.Empty,
                });
            }
            return rows
                .OrderByDescending(r => r.Date, StringComparer.Ordinal)
                .ThenBy(r => r.Accession, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
