using HemoIQ.Data;

namespace HemoIQ.Services
{
    /// <summary>Every field the Specimen dialog edits, as entered.</summary>
    public sealed class SpecimenEdit
    {
        public string AccessionNumber { get; init; } = string.Empty;
        public string Type { get; init; } = "serum";
        public string? ExpirationDate { get; init; }
        public bool IsActive { get; init; } = true;
        public string? Notes { get; init; }
        public string? Phenotype { get; init; }
        public string? Genotype { get; init; }
        public string? PreviousAntibodies { get; init; }
        public string? DatResult { get; init; }
        public string? DatIgg { get; init; }
        public string? DatC3 { get; init; }
        public string TransfusionStatus { get; init; } = Models.TransfusionStatus.Unknown;
        public string? LastTransfusionDate { get; init; }
        public string? Mrn { get; init; }
        public string? SourceAccession { get; init; }
        public string? CollectedAt { get; init; }
        public string? ReceivedAt { get; init; }
        public string? Collector { get; init; }
        public string? CollectionLocation { get; init; }
        public string? OrderingLocation { get; init; }
        public string? OrderingProvider { get; init; }
        public string PregnancyStatus { get; init; } = Models.PregnancyStatus.Unknown;
        public string? PregnancyEndDate { get; init; }
        public string? RhigDate { get; init; }
    }

    /// <summary>
    /// Saves a specimen and all of its dialog fields in one transaction. Each step is
    /// still its own validated, audited update, but if any step is refused (an invalid
    /// value, a locked record, a bad eluate link), nothing from the save is kept: no
    /// half-created specimen and no audit entries for changes that did not happen.
    /// </summary>
    public static class SpecimenSaveService
    {
        public static void Save(DatabaseService db, SpecimenEdit e, bool isNew)
        {
            db.RunInTransaction(() =>
            {
                if (isNew)
                    db.AddSpecimen(e.AccessionNumber, e.Type, e.ExpirationDate, e.IsActive,
                        e.Notes, e.Phenotype, e.PreviousAntibodies, e.DatResult, genotype: e.Genotype);
                else
                    db.UpdateSpecimen(e.AccessionNumber, e.Type, e.ExpirationDate, e.IsActive,
                        e.Notes, e.Phenotype, e.PreviousAntibodies, e.DatResult, e.Genotype);
                db.UpdateSpecimenTransfusion(e.AccessionNumber, e.TransfusionStatus, e.LastTransfusionDate);
                db.UpdateSpecimenDatSplit(e.AccessionNumber, e.DatIgg, e.DatC3);
                db.UpdateSpecimenMrn(e.AccessionNumber, e.Mrn);
                db.SetEluateSource(e.AccessionNumber, e.SourceAccession);
                db.UpdateSpecimenCollection(e.AccessionNumber, e.CollectedAt, e.ReceivedAt);
                db.UpdateSpecimenOrigin(e.AccessionNumber, e.Collector, e.CollectionLocation,
                    e.OrderingLocation, e.OrderingProvider);
                db.UpdateSpecimenPregnancy(e.AccessionNumber, e.PregnancyStatus, e.PregnancyEndDate, e.RhigDate);
            });
        }
    }
}
