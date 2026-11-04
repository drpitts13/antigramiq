using System;
using System.Globalization;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Checks specimen dating against the transfusion record. When the patient was
    /// transfused within the configured look-back, a pretransfusion specimen should be
    /// used no later than collection date + the lab's dating days. Review note only;
    /// it never changes the recorded expiration.
    /// </summary>
    public static class SpecimenDating
    {
        public const string Format = "yyyy-MM-dd HH:mm";

        /// <summary>
        /// Returns a REVIEW sentence when dating cannot be confirmed or the recorded
        /// expiration is later than allowed; otherwise null.
        /// </summary>
        public static string? Check(Specimen s, int datingDays, int lookbackDays)
        {
            var reference = TryParse(s.CollectedAt, out var collected) ? collected
                : TryParse(s.CreatedDate, out var created) ? created : DateTime.Today;
            var transfusionText = Trigger(s, reference.Date, lookbackDays);
            if (transfusionText == null) return null;

            if (!TryParse(s.CollectedAt, out var collectedAt))
                return $"Specimen dating cannot be checked: the patient has {transfusionText} within {lookbackDays} days, " +
                       $"but no collection date and time is recorded. Record it to confirm the {datingDays}-day dating. Not an order.";

            var latest = collectedAt.Date.AddDays(datingDays);
            if (!TryParse(s.ExpirationDate, out var exp))
                return $"Specimen dating: with {transfusionText} within {lookbackDays} days, this specimen should not be used after " +
                       $"{latest:yyyy-MM-dd} (collected {collectedAt:yyyy-MM-dd HH:mm} + {datingDays} days), and no expiration is recorded. Not an order.";

            if (exp.Date > latest)
                return $"Specimen dating: expiration {exp:yyyy-MM-dd} is later than collection {collectedAt:yyyy-MM-dd HH:mm} + {datingDays} days " +
                       $"({latest:yyyy-MM-dd}) allowed after {transfusionText} within {lookbackDays} days. Review the expiration. Not an order.";
            return null;
        }

        /// <summary>
        /// Describes why dating applies (transfusion and/or pregnancy within the look-back),
        /// or null when neither applies. A transfusion or pregnancy with no date counts.
        /// </summary>
        private static string? Trigger(Specimen s, DateTime reference, int lookbackDays)
        {
            var reasons = new System.Collections.Generic.List<string>();
            if (TransfusionStatus.Normalize(s.TransfusionStatus) == TransfusionStatus.Transfused)
            {
                if (!TryParse(s.LastTransfusionDate, out var last))
                    reasons.Add("a transfusion with no recorded date");
                else if ((reference - last.Date).TotalDays <= lookbackDays)
                    reasons.Add($"last transfusion {last:yyyy-MM-dd}");
            }
            switch (PregnancyStatus.Normalize(s.PregnancyStatus))
            {
                case PregnancyStatus.Pregnant:
                    reasons.Add("current pregnancy");
                    break;
                case PregnancyStatus.Recent when TryParse(s.PregnancyEndDate, out var end):
                    if ((reference - end.Date).TotalDays <= lookbackDays)
                        reasons.Add($"pregnancy ended {end:yyyy-MM-dd}");
                    break;
                case PregnancyStatus.Recent:
                    reasons.Add("a recent pregnancy with no recorded end date");
                    break;
            }
            return reasons.Count == 0 ? null : string.Join(" and ", reasons);
        }

        /// <summary>
        /// REVIEW note when anti-D is suspected or confirmed and Rh immune globulin was given
        /// within <paramref name="windowDays"/> before collection; otherwise null.
        /// </summary>
        public static string? PassiveAntiD(Specimen s, bool antiDSuspectedOrConfirmed, int windowDays)
        {
            if (!antiDSuspectedOrConfirmed || !TryParse(s.RhigDate, out var rhig)) return null;
            var reference = TryParse(s.CollectedAt, out var c) ? c.Date
                : TryParse(s.CreatedDate, out var r) ? r.Date : DateTime.Today;
            var days = (reference - rhig.Date).TotalDays;
            if (days < 0 || days > windowDays) return null;
            return $"Anti-D is suspected or confirmed, and Rh immune globulin was given {rhig:yyyy-MM-dd} ({days:0} day(s) before collection, " +
                   $"within the configured {windowDays}-day window). Passive anti-D from RhIG is possible: review RhIG history, " +
                   "strength or titer, and timing before reporting an immune alloanti-D. Not a diagnosis.";
        }

        /// <summary>Validates and normalizes a collection or receipt time; null or blank clears it.</summary>
        public static string? Normalize(string? text, string label, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!TryParse(text, out var t))
                throw new ArgumentException($"{label} '{text}' is not a valid date and time.");
            if (t > now.AddMinutes(5))
                throw new ArgumentException($"{label} cannot be in the future.");
            return t.ToString(Format, CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string? text, out DateTime value) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
