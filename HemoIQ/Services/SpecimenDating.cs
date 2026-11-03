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
            if (TransfusionStatus.Normalize(s.TransfusionStatus) != TransfusionStatus.Transfused)
                return null;

            var reference = TryParse(s.CollectedAt, out var collected) ? collected
                : TryParse(s.CreatedDate, out var created) ? created : DateTime.Today;
            if (TryParse(s.LastTransfusionDate, out var last) && (reference.Date - last.Date).TotalDays > lookbackDays)
                return null;

            var transfusionText = TryParse(s.LastTransfusionDate, out var lt)
                ? $"last transfusion {lt:yyyy-MM-dd}"
                : "a transfusion with no recorded date";

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
