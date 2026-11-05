using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class TechnologistWorkload
    {
        /// <summary>Normalized initials as recorded on the investigation.</summary>
        public string Initials { get; init; } = string.Empty;
        /// <summary>Registered user name for these initials, or empty when not registered.</summary>
        public string Name { get; init; } = string.Empty;
        public int Confirmed { get; init; }
        /// <summary>Confirmed identifications naming more than one antibody.</summary>
        public int MultipleAntibodies { get; init; }
        public int Reviewed { get; init; }
        /// <summary>Median received to confirmed, in days, for this technologist's confirmations.</summary>
        public double? MedianConfirmDays { get; init; }

        public string Display => string.IsNullOrWhiteSpace(Name) ? Initials : $"{Name} ({Initials})";
        public string MedianConfirmDisplay => MedianConfirmDays is double d ? $"{d:F1} d" : "—";
        public int Total => Confirmed + Reviewed;
    }

    public sealed class LocationWorkload
    {
        public string Location { get; init; } = string.Empty;
        public int Received { get; init; }
        public int Confirmed { get; init; }
        public int Open { get; init; }
        public double? MedianConfirmDays { get; init; }
        public string MedianConfirmDisplay => MedianConfirmDays is double d ? $"{d:F1} d" : "—";
    }

    /// <summary>
    /// Work done per technologist in a date range. Confirmations are counted by the
    /// confirmed date and attributed to Identified by; supervisor reviews are counted by
    /// the review date and attributed to Reviewed by. Cancelled investigations are
    /// excluded. Initials are matched case-insensitively and names come from the user
    /// registry when the initials are registered.
    /// </summary>
    public static class WorkloadService
    {
        public static List<TechnologistWorkload> Compute(IEnumerable<Specimen> specimens, DateTime? from, DateTime? to,
            IEnumerable<AppUser>? users = null)
        {
            var names = (users ?? Enumerable.Empty<AppUser>())
                .Where(u => !string.IsNullOrWhiteSpace(u.Initials))
                .GroupBy(u => LabSettings.NormalizeInitials(u.Initials), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (g.FirstOrDefault(u => u.Active) ?? g.First()).Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            var confirmations = new Dictionary<string, List<Specimen>>(StringComparer.OrdinalIgnoreCase);
            var reviews = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in specimens.Where(s => !s.IsCancelled))
            {
                var tech = LabSettings.NormalizeInitials(s.IdentifiedBy);
                if (s.HasFinalCall && tech.Length > 0 && InRange(s.IdentifiedAt, from, to))
                {
                    if (!confirmations.TryGetValue(tech, out var list)) confirmations[tech] = list = new List<Specimen>();
                    list.Add(s);
                }
                var reviewer = LabSettings.NormalizeInitials(s.ReviewedBy);
                if (s.HasFinalCall && reviewer.Length > 0 && InRange(s.ReviewedAt, from, to))
                    reviews[reviewer] = reviews.GetValueOrDefault(reviewer) + 1;
            }

            return confirmations.Keys.Union(reviews.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(initials =>
                {
                    var mine = confirmations.GetValueOrDefault(initials) ?? new List<Specimen>();
                    var days = mine
                        .Select(s => (s, received: TurnaroundService.ReceivedTime(s)))
                        .Where(x => TryDate(x.s.IdentifiedAt, out var c) && c >= x.received && x.received > DateTime.MinValue)
                        .Select(x => { TryDate(x.s.IdentifiedAt, out var c); return (c - x.received).TotalDays; })
                        .ToList();
                    return new TechnologistWorkload
                    {
                        Initials = initials.ToUpperInvariant(),
                        Name = names.GetValueOrDefault(initials) ?? string.Empty,
                        Confirmed = mine.Count,
                        MultipleAntibodies = mine.Count(s => AntibodyCount(s.FinalAntibodies) > 1),
                        Reviewed = reviews.GetValueOrDefault(initials),
                        MedianConfirmDays = TurnaroundService.Stats(days).Median,
                    };
                })
                .OrderByDescending(w => w.Total)
                .ThenBy(w => w.Initials, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public const string NoLocation = "(not recorded)";

        /// <summary>
        /// Investigations received in the range, grouped by ordering location (trimmed,
        /// case-insensitive; blank is "(not recorded)"). Counts received, confirmed and
        /// still-open (active, unconfirmed) cases, with the median received-to-confirmed
        /// time. Cancelled investigations are excluded. Busiest location first.
        /// </summary>
        public static List<LocationWorkload> ComputeByLocation(IEnumerable<Specimen> specimens, DateTime? from, DateTime? to)
        {
            return specimens
                .Where(s => !s.IsCancelled && InRange(s.CreatedDate, from, to))
                .GroupBy(s => string.IsNullOrWhiteSpace(s.OrderingLocation) ? NoLocation : s.OrderingLocation.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var days = g.Where(s => s.HasFinalCall)
                        .Select(s => (s, received: TurnaroundService.ReceivedTime(s)))
                        .Where(x => TryDate(x.s.IdentifiedAt, out var c) && c >= x.received && x.received > DateTime.MinValue)
                        .Select(x => { TryDate(x.s.IdentifiedAt, out var c); return (c - x.received).TotalDays; })
                        .ToList();
                    return new LocationWorkload
                    {
                        Location = g.Key,
                        Received = g.Count(),
                        Confirmed = g.Count(s => s.HasFinalCall),
                        Open = g.Count(s => s.IsActive && !s.HasFinalCall),
                        MedianConfirmDays = TurnaroundService.Stats(days).Median,
                    };
                })
                .OrderBy(w => w.Location == NoLocation)
                .ThenByDescending(w => w.Received)
                .ThenBy(w => w.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int AntibodyCount(string? finalAntibodies) => FinalAntibodyParser.Split(finalAntibodies).Count;

        private static bool InRange(string? text, DateTime? from, DateTime? to) =>
            TryDate(text, out var d) &&
            (from == null || d.Date >= from.Value.Date) &&
            (to == null || d.Date <= to.Value.Date);

        private static bool TryDate(string? text, out DateTime d) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }
}
