using System;
using System.Collections.Generic;
using System.Globalization;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class TransfusionAssessment
    {
        public bool PhenotypeUnreliable { get; init; }
        /// <summary>True after an allogeneic HPC transplant: a recorded genotype may be the donor's.</summary>
        public bool GenotypeMayBeDonor { get; init; }
        /// <summary>Sentence explaining which recorded value decided reliability and why.</summary>
        public string Explanation { get; init; } = string.Empty;
        /// <summary>"structured", "notes", or "none".</summary>
        public string Source { get; init; } = "none";
        /// <summary>Normalized <see cref="TransfusionStatus"/> code that was assessed.</summary>
        public string Status { get; init; } = TransfusionStatus.Unknown;
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
                return WithStatus(AssessTransfused(lastTransfusionDate, specimenDate, windowDays), code);

            return WithStatus(AssessUntransfused(code, notesHint), code);
        }

        private static TransfusionAssessment WithStatus(TransfusionAssessment a, string code) => new()
        {
            PhenotypeUnreliable = a.PhenotypeUnreliable,
            Explanation = a.Explanation,
            Source = a.Source,
            Status = code,
        };

        private static TransfusionAssessment AssessUntransfused(string code, bool notesHint)
        {
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

        /// <summary>
        /// Names a mixed-field autocontrol in each phase and reads it against
        /// the transfusion record. Two red-cell populations in the patient's
        /// own cells are a review signal, not an identification.
        /// </summary>
        public static List<string> DescribeMixedFieldAutocontrol(
            IEnumerable<Reaction> reactions, TransfusionAssessment? transfusion)
        {
            var notes = new List<string>();
            foreach (var rxn in reactions)
            {
                if (!string.Equals(rxn.CellNumber, "AC", StringComparison.OrdinalIgnoreCase)) continue;
                var phases = new List<string>();
                foreach (var (phase, val) in AutocontrolPhases(rxn))
                    if (ReactionGrade.Normalize(val) == ReactionGrade.MixedField && !phases.Contains(phase))
                        phases.Add(phase);
                if (phases.Count == 0) continue;

                var where = $"Autocontrol {string.Join(", ", phases)} is mixed-field (MF)";
                notes.Add(MixedFieldSentence(where, transfusion));
            }
            return notes;
        }

        /// <summary>
        /// Names DAT results graded mixed field and reads them against the
        /// transfusion record, using the same wording as the autocontrol.
        /// </summary>
        public static List<string> DescribeMixedFieldDat(DatProfile dat, TransfusionAssessment? transfusion)
        {
            var mf = dat.MixedFieldResults;
            if (mf.Count == 0) return new List<string>();
            return new List<string> { MixedFieldSentence($"DAT {string.Join(", ", mf)} is mixed-field (MF)", transfusion) };
        }

        private static IEnumerable<(string Phase, string Value)> AutocontrolPhases(Reaction rxn)
        {
            yield return ("IS", rxn.IS);
            yield return ("37°C", rxn.C37);
            yield return ("AHG", rxn.AHG);
            foreach (var (phase, val) in rxn.ExtraPhases)
                yield return (phase, val);
        }

        private static string MixedFieldSentence(string where, TransfusionAssessment? t)
        {
            const string tail = " This is evidence for review, not a diagnosis.";
            if (t != null && t.PhenotypeUnreliable)
                return $"{where} with a recent transfusion recorded. {t.Explanation} " +
                       "A mixed-field autocontrol after transfusion can mean alloantibody coating surviving donor cells " +
                       "(possible delayed serologic or hemolytic transfusion reaction). Consider a DAT and an eluate tested against the panel." + tail;
            if (t != null && t.Status == TransfusionStatus.NotTransfused)
                return $"{where}, but transfusion status is Not transfused. Two red-cell populations without a recorded transfusion " +
                       "warrant review of transfusion history elsewhere, stem-cell or marrow transplant, and the specimen itself." + tail;
            if (t != null && t.Status == TransfusionStatus.Transfused)
                return $"{where}. {t.Explanation} Donor cells are less likely after this interval; " +
                       "review for a more recent transfusion, transplant, or a specimen problem." + tail;
            return $"{where} and transfusion status is Unknown. A mixed-field autocontrol often follows recent transfusion; " +
                   "record the transfusion status and date on the specimen, and consider a DAT and an eluate." + tail;
        }

        private static bool TryDate(string? text, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
