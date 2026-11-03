using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class DurationStats
    {
        public int Count { get; init; }
        public double? Median { get; init; }
        public double? Percentile90 { get; init; }

        public string MedianDisplay(string unit) => Median is double m ? $"{m:F1} {unit}" : "—";
        public string P90Display(string unit) => Percentile90 is double p ? $"{p:F1} {unit}" : "—";
    }

    public sealed class OverdueInvestigation
    {
        public string Accession { get; init; } = string.Empty;
        public string Received { get; init; } = string.Empty;
        public int AgeDays { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public sealed class TurnaroundResult
    {
        /// <summary>Received date to confirmed identification, in days.</summary>
        public DurationStats ReceivedToConfirmed { get; init; } = new();
        /// <summary>Confirmed identification to supervisor review, in hours.</summary>
        public DurationStats ConfirmedToReviewed { get; init; } = new();
        /// <summary>Open (unconfirmed, not cancelled, active) investigations older than the threshold, oldest first.</summary>
        public List<OverdueInvestigation> Overdue { get; init; } = new();
        public int ThresholdDays { get; init; }
    }

    /// <summary>
    /// Turnaround metrics for investigations received within a date range. Received
    /// time is the received date at midnight (only the date is recorded), so
    /// received-to-confirmed is an upper bound. Cancelled investigations are excluded.
    /// Percentiles use the nearest-rank method.
    /// </summary>
    public static class TurnaroundService
    {
        public static TurnaroundResult Compute(IEnumerable<Specimen> specimens, DateTime? from, DateTime? to,
            int thresholdDays, DateTime now, Func<Specimen, string>? statusOf = null)
        {
            var inRange = specimens.Where(s => !s.IsCancelled && TryDate(s.CreatedDate, out var r) &&
                                               (from == null || r >= from.Value.Date) &&
                                               (to == null || r <= to.Value.Date))
                .ToList();

            var toConfirm = new List<double>();
            var toReview = new List<double>();
            foreach (var s in inRange)
            {
                TryDate(s.CreatedDate, out var received);
                if (s.HasFinalCall && TryDate(s.IdentifiedAt, out var confirmed) && confirmed >= received)
                {
                    toConfirm.Add((confirmed - received).TotalDays);
                    if (s.IsReviewed && TryDate(s.ReviewedAt, out var reviewed) && reviewed >= confirmed)
                        toReview.Add((reviewed - confirmed).TotalHours);
                }
            }

            var overdue = inRange
                .Where(s => s.IsActive && !s.HasFinalCall)
                .Select(s => { TryDate(s.CreatedDate, out var r); return (s, age: (int)(now.Date - r.Date).TotalDays); })
                .Where(x => x.age > thresholdDays)
                .OrderByDescending(x => x.age)
                .ThenBy(x => x.s.AccessionNumber, StringComparer.OrdinalIgnoreCase)
                .Select(x => new OverdueInvestigation
                {
                    Accession = x.s.AccessionNumber,
                    Received = x.s.CreatedDate.Length >= 10 ? x.s.CreatedDate[..10] : x.s.CreatedDate,
                    AgeDays = x.age,
                    Status = statusOf?.Invoke(x.s) ?? string.Empty,
                })
                .ToList();

            return new TurnaroundResult
            {
                ReceivedToConfirmed = Stats(toConfirm),
                ConfirmedToReviewed = Stats(toReview),
                Overdue = overdue,
                ThresholdDays = thresholdDays,
            };
        }

        public static DurationStats Stats(IReadOnlyCollection<double> values)
        {
            if (values.Count == 0) return new DurationStats();
            var sorted = values.OrderBy(v => v).ToList();
            return new DurationStats
            {
                Count = sorted.Count,
                Median = sorted.Count % 2 == 1
                    ? sorted[sorted.Count / 2]
                    : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2,
                Percentile90 = NearestRank(sorted, 0.90),
            };
        }

        public static double NearestRank(IReadOnlyList<double> sorted, double p)
        {
            var rank = (int)Math.Ceiling(p * sorted.Count);
            return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
        }

        private static bool TryDate(string? text, out DateTime d) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }
}
