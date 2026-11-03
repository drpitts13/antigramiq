using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Reads eluate specimens linked to a source specimen and explains what their
    /// results mean for the source investigation. Notes are reviewable evidence;
    /// they never add, remove, or rule out a candidate antibody.
    /// </summary>
    public static class EluateEvidence
    {
        public const int MinEvaluatedCells = 3;

        public static List<string> Describe(DatabaseService db, Specimen source,
            IEnumerable<string> sourceSuspected, TransfusionAssessment? transfusion)
        {
            var notes = new List<string>();
            var suspected = new HashSet<string>(sourceSuspected, StringComparer.OrdinalIgnoreCase);
            foreach (var eluate in db.GetEluatesFor(source.AccessionNumber))
                notes.Add(DescribeOne(db, source, eluate, suspected, transfusion));
            return notes;
        }

        private static string DescribeOne(DatabaseService db, Specimen source, Specimen eluate,
            HashSet<string> suspected, TransfusionAssessment? transfusion)
        {
            const string tail = " This is evidence for review, not a diagnosis.";
            var (reactive, evaluated) = CountCells(db, eluate.AccessionNumber);
            var head = $"Eluate {eluate.AccessionNumber}";

            if (evaluated == 0)
                return $"{head} is linked to this specimen but has no reactions entered yet.";

            var counts = $"{reactive} of {evaluated} evaluated cells reactive";
            if (evaluated >= MinEvaluatedCells && reactive == 0)
            {
                var text = $"{head} is nonreactive ({counts}).";
                if (source.Dat.IsPositive)
                {
                    text += $" The DAT is positive ({source.Dat.Describe()}). A nonreactive eluate with a positive DAT can occur with " +
                            "drug-related antibodies, antibodies to antigens not on the panel (for example ABO antibodies from " +
                            "ABO-incompatible plasma), or complement-only coating.";
                    if (source.Dat.C3Only)
                        text += " Only C3 is reactive; elution recovers IgG, so a nonreactive eluate is expected here.";
                }
                return text + tail;
            }

            if (evaluated >= 4 && reactive * 5 >= evaluated * 4)
                return $"{head} is panreactive ({counts}). A panreactive eluate supports IgG autoantibody on the patient's own cells. " +
                       "If the patient was recently transfused, an alloantibody may also be present; consider adsorption before ruling one out." + tail;

            var specificities = eluate.HasFinalCall
                ? FinalAntibodyParser.Split(eluate.FinalAntibodies).Select(a => (a, "confirmed")).ToList()
                : db.GetSpecimenAntibodies(eluate.AccessionNumber)
                    .OrderByDescending(a => a.Probability)
                    .Select(a => (a.Antibody, "suspected")).ToList();

            if (specificities.Count == 0)
                return $"{head} shows reactivity ({counts}) but no specificity has been analyzed or confirmed yet. Run analysis on the eluate." + tail;

            var named = string.Join(", ", specificities.Select(s => $"{s.Item1} ({s.Item2})"));
            var note = $"{head} shows {named}; {counts}.";
            if (transfusion?.PhenotypeUnreliable == true)
                note += " Antibody recovered from the patient's red cells after a recent transfusion supports alloantibody coating " +
                        "transfused donor cells (possible delayed hemolytic or serologic transfusion reaction).";
            var notInSerum = specificities.Select(s => s.Item1)
                .Where(a => a.StartsWith("anti-", StringComparison.OrdinalIgnoreCase) && !suspected.Contains(a))
                .ToList();
            if (notInSerum.Count > 0)
                note += $" {string.Join(", ", notInSerum)} is in the eluate but not suspected in {source.Type} {source.AccessionNumber}; " +
                        "it may be bound to the red cells. Review before relying on serum rule-outs.";
            return note + tail;
        }

        /// <summary>Reactive and evaluated (graded) panel cells across the eluate's untreated runs.</summary>
        public static (int Reactive, int Evaluated) CountCells(DatabaseService db, string accession)
        {
            int reactive = 0, evaluated = 0;
            foreach (var run in db.GetAllSpecimenRuns(accession).Where(r => r.IsUntreated))
            {
                foreach (var rxn in db.GetReactions(run.RunId))
                {
                    if (string.Equals(rxn.CellNumber, "AC", StringComparison.OrdinalIgnoreCase)) continue;
                    var graded = new[] { rxn.IS, rxn.C37, rxn.AHG }.Concat(rxn.ExtraPhases.Values)
                        .Any(v => !string.IsNullOrWhiteSpace(v) && ReactionGrade.Normalize(v) != ReactionGrade.NotTested);
                    if (!graded) continue;
                    evaluated++;
                    if (rxn.IsPositive) reactive++;
                }
            }
            return (reactive, evaluated);
        }
    }
}
