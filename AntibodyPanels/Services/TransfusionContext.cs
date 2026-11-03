using System;
using System.Globalization;
using AntibodyPanels.Models;

namespace AntibodyPanels.Services
{
    public sealed class TransfusionAssessment
    {
        public bool PhenotypeUnreliable { get; init; }
        /// <summary>Sentence explaining which recorded value decided reliability and why.</summary>
        public string Explanation { get; init; } = string.Empty;
        /// <summary>"structured", "notes", or "none".</summary>
        public string Source { get; init; } = "none";
    }

    /// <summary>
    /// Decides whether serologic patient typing may be donor-cell typing.
    /// The structured status and last-transfusion date take precedence over
    /// free-text notes. Notes keywords decide only when the status is
    /// Unknown; under "Not transfused" they are named for review. Evidence only.
    /// </summary>
    public static class TransfusionContext
    {
        public static TransfusionAssessment Assess(string? status, string? lastTransfusionDate,
            string? specimenDate, int windowDays, string? phenotype, string? notes)
        {
            var code = TransfusionStatus.Normalize(status);
            var notesHint = PatientTypingParser.LooksRecentlyTransfused(phenotype) ||
                            PatientTypingParser.LooksRecentlyTransfused(notes);

            if (code == TransfusionStatus.Transfused)
                return AssessTransfused(lastTransfusionDate, specimenDate, windowDays);

            if (code == TransfusionStatus.NotTransfused)
            {
                if (notesHint)
                    return new TransfusionAssessment
                    {
                        Source = "structured",
                        Explanation =
                            "Transfusion status is recorded as Not transfused, so serologic patient typing is used as evidence. " +
                            "The phenotype or notes mention transfusion; review that text and correct the status if the patient was transfused.",
                    };
                return new TransfusionAssessment
                {
                    Source = "structured",
                    Explanation = "Transfusion status is recorded as Not transfused, so serologic patient typing is used as evidence.",
                };
            }

            if (notesHint)
                return new TransfusionAssessment
                {
                    PhenotypeUnreliable = true,
                    Source = "notes",
                    Explanation =
                        "Transfusion status is Unknown, and the phenotype or notes mention transfusion or unreliable typing, " +
                        "so patient typing is treated as uninterpretable. Record the transfusion status and date on the specimen to replace this keyword check.",
                };

            return new TransfusionAssessment
            {
                Source = "none",
                Explanation = "Transfusion status is Unknown. Serologic patient typing is used as evidence; record the status if the patient may have been transfused.",
            };
        }

        private static TransfusionAssessment AssessTransfused(string? lastDate, string? specimenDate, int windowDays)
        {
            if (!TryDate(lastDate, out var last))
                return new TransfusionAssessment
                {
                    PhenotypeUnreliable = true,
                    Source = "structured",
                    Explanation =
                        "Transfusion status is recorded as Transfused with no valid last-transfusion date, " +
                        "so patient typing is treated as uninterpretable (possible donor cells).",
                };

            var reference = TryDate(specimenDate, out var s) ? s : DateTime.Today;
            var days = (reference - last).Days;
            var lastText = last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (days < 0)
                return new TransfusionAssessment
                {
                    PhenotypeUnreliable = true,
                    Source = "structured",
                    Explanation =
                        $"The last transfusion ({lastText}) is recorded after the specimen date. " +
                        "Patient typing is treated as uninterpretable until the dates are reviewed.",
                };

            if (days <= windowDays)
                return new TransfusionAssessment
                {
                    PhenotypeUnreliable = true,
                    Source = "structured",
                    Explanation =
                        $"Last transfusion {lastText} was {days} day(s) before the specimen, within the configured {windowDays}-day window. " +
                        "Serologic patient typing may reflect donor cells and is treated as uninterpretable.",
                };

            return new TransfusionAssessment
            {
                Source = "structured",
                Explanation =
                    $"Last transfusion {lastText} was {days} day(s) before the specimen, outside the configured {windowDays}-day window, " +
                    "so serologic patient typing is used as evidence.",
            };
        }

        private static bool TryDate(string? text, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
