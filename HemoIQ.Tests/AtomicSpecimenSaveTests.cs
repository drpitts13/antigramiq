using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class AtomicSpecimenSaveTests
{
    private static int AuditCount(IsolatedDatabase iso, string acc) =>
        iso.Db.GetAuditEventsForEntity("specimen", acc).Count;

    [Fact]
    public void NewSpecimen_WithEveryField_IsSavedTogether()
    {
        using var iso = new IsolatedDatabase();
        SpecimenSaveService.Save(iso.Db, new SpecimenEdit
        {
            AccessionNumber = "AT-1", Mrn = "m-1", DatIgg = "2+",
            TransfusionStatus = TransfusionStatus.Transfused, LastTransfusionDate = "2026-10-30",
            CollectedAt = "2026-11-01 08:00", ReceivedAt = "2026-11-01 09:00",
            OrderingLocation = "ICU", PregnancyStatus = PregnancyStatus.NotPregnant,
        }, isNew: true);

        var s = iso.Db.GetSpecimen("AT-1")!;
        Assert.Equal("M-1", s.Mrn);
        Assert.Equal("2+", s.DatIgg);
        Assert.Equal("2026-10-30", s.LastTransfusionDate);
        Assert.Equal("ICU", s.OrderingLocation);
        Assert.Equal(PregnancyStatus.NotPregnant, s.PregnancyStatus);
    }

    [Fact]
    public void NewSpecimen_LaterStepRefused_LeavesNoSpecimenAndNoAudit()
    {
        using var iso = new IsolatedDatabase();
        var ex = Assert.Throws<ArgumentException>(() => SpecimenSaveService.Save(iso.Db, new SpecimenEdit
        {
            AccessionNumber = "AT-2", Type = "eluate", Mrn = "M-2",
            TransfusionStatus = TransfusionStatus.Transfused, LastTransfusionDate = "2026-10-30",
            SourceAccession = "DOES-NOT-EXIST",
        }, isNew: true));

        Assert.Contains("DOES-NOT-EXIST", ex.Message);
        Assert.Null(iso.Db.GetSpecimen("AT-2"));
        Assert.Equal(0, AuditCount(iso, "AT-2"));
    }

    [Fact]
    public void Edit_LaterStepRefused_KeepsEveryEarlierValue()
    {
        using var iso = new IsolatedDatabase();
        SpecimenSaveService.Save(iso.Db, new SpecimenEdit
        {
            AccessionNumber = "AT-3", Mrn = "M-3", Notes = "original", Collector = "AB",
        }, isNew: true);
        var auditsBefore = AuditCount(iso, "AT-3");

        // Received before collected is refused by the collection step, after the
        // notes, transfusion, DAT and MRN steps have already run.
        Assert.Throws<ArgumentException>(() => SpecimenSaveService.Save(iso.Db, new SpecimenEdit
        {
            AccessionNumber = "AT-3", Mrn = "M-99", Notes = "changed", DatIgg = "3+",
            TransfusionStatus = TransfusionStatus.Transfused, LastTransfusionDate = "2026-10-30",
            CollectedAt = "2026-11-02 10:00", ReceivedAt = "2026-11-02 09:00", Collector = "ZZ",
        }, isNew: false));

        var s = iso.Db.GetSpecimen("AT-3")!;
        Assert.Equal("original", s.Notes);
        Assert.Equal("M-3", s.Mrn);
        Assert.Null(s.DatIgg);
        Assert.Equal(TransfusionStatus.Unknown, s.TransfusionStatus);
        Assert.Equal("AB", s.Collector);
        Assert.Equal(auditsBefore, AuditCount(iso, "AT-3"));
    }

    [Fact]
    public void Edit_LockedSpecimen_ChangesNothing()
    {
        using var iso = new IsolatedDatabase();
        SpecimenSaveService.Save(iso.Db, new SpecimenEdit { AccessionNumber = "AT-4", Notes = "n" }, isNew: true);
        iso.Db.SetSpecimenFinalCall("AT-4", "anti-E", null, "TE");

        Assert.Throws<RecordLockedException>(() => SpecimenSaveService.Save(iso.Db,
            new SpecimenEdit { AccessionNumber = "AT-4", Notes = "edited" }, isNew: false));
        Assert.Equal("n", iso.Db.GetSpecimen("AT-4")!.Notes);
    }

    [Fact]
    public void RunInTransaction_Nests_AndInnerRollbackKeepsOuterWork()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.RunInTransaction(() =>
        {
            iso.Db.AddSpecimen("AT-5", "serum", null);
            Assert.Throws<InvalidOperationException>(() => iso.Db.RunInTransaction(() =>
            {
                iso.Db.AddSpecimen("AT-6", "serum", null);
                throw new InvalidOperationException("inner");
            }));
        });

        Assert.NotNull(iso.Db.GetSpecimen("AT-5"));
        Assert.Null(iso.Db.GetSpecimen("AT-6"));

        // The connection is usable normally afterwards.
        iso.Db.AddSpecimen("AT-7", "serum", null);
        Assert.NotNull(iso.Db.GetSpecimen("AT-7"));
    }
}
