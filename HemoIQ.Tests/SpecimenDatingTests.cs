using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class SpecimenDatingTests
{
    private static Specimen Transfused(string? collected, string? exp, string? lastTransfusion = "2026-10-20") => new()
    {
        AccessionNumber = "SD",
        CreatedDate = "2026-11-01",
        CollectedAt = collected,
        ExpirationDate = exp,
        TransfusionStatus = TransfusionStatus.Transfused,
        LastTransfusionDate = lastTransfusion,
    };

    // ── Dating rule ─────────────────────────────────────────────────────

    [Fact]
    public void NotTransfused_OrOutsideLookback_GivesNoNote()
    {
        var s = Transfused("2026-11-01 08:00", "2026-11-30");
        s.TransfusionStatus = TransfusionStatus.NotTransfused;
        Assert.Null(SpecimenDating.Check(s, 3, 90));

        var old = Transfused("2026-11-01 08:00", "2026-11-30", lastTransfusion: "2026-01-01");
        Assert.Null(SpecimenDating.Check(old, 3, 90));
    }

    [Fact]
    public void ExpirationWithinCollectionPlusDays_IsFine_LaterIsFlagged()
    {
        Assert.Null(SpecimenDating.Check(Transfused("2026-11-01 08:00", "2026-11-04"), 3, 90));

        var note = SpecimenDating.Check(Transfused("2026-11-01 08:00", "2026-11-08"), 3, 90);
        Assert.NotNull(note);
        Assert.Contains("expiration 2026-11-08 is later than collection 2026-11-01 08:00 + 3 days (2026-11-04)", note);
        Assert.Contains("last transfusion 2026-10-20", note);
    }

    [Fact]
    public void MissingCollection_OrMissingExpiration_AfterRecentTransfusion_IsFlagged()
    {
        Assert.Contains("no collection date and time is recorded",
            SpecimenDating.Check(Transfused(null, "2026-11-04"), 3, 90));
        Assert.Contains("no expiration is recorded",
            SpecimenDating.Check(Transfused("2026-11-01 08:00", null), 3, 90));
        Assert.Contains("a transfusion with no recorded date",
            SpecimenDating.Check(Transfused(null, null, lastTransfusion: null), 3, 90));
    }

    [Fact]
    public void Normalize_RefusesFutureAndInvalid_AndFormats()
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0);
        Assert.Equal("2026-11-03 07:05", SpecimenDating.Normalize("2026-11-03 7:05", "Collection time", now));
        Assert.Null(SpecimenDating.Normalize("  ", "Collection time", now));
        Assert.Throws<ArgumentException>(() => SpecimenDating.Normalize("2026-11-04 08:00", "Collection time", now));
        Assert.Throws<ArgumentException>(() => SpecimenDating.Normalize("soon", "Collection time", now));
    }

    // ── Persistence, audit, analysis, worksheet ─────────────────────────

    [Fact]
    public void UpdateCollection_PersistsAudits_AndRejectsReceivedBeforeCollected()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SD-1", "serum", null);
        Assert.Throws<ArgumentException>(() =>
            iso.Db.UpdateSpecimenCollection("SD-1", "2026-11-01 10:00", "2026-11-01 09:00"));

        iso.Db.UpdateSpecimenCollection("SD-1", "2026-11-01 08:00", "2026-11-01 09:30");
        var s = iso.Db.GetSpecimen("SD-1")!;
        Assert.Equal("2026-11-01 08:00", s.CollectedAt);
        Assert.Equal("2026-11-01 09:30", s.ReceivedAt);
        Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "SD-1"), e => e.Action == "update_collection");

        iso.Db.UpdateSpecimenCollection("SD-1", "2026-11-01 08:00", "2026-11-01 09:30"); // unchanged
        Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "SD-1"), e => e.Action == "update_collection");

        iso.Db.SetSpecimenFinalCall("SD-1", "anti-E", null, "DP");
        Assert.Throws<RecordLockedException>(() => iso.Db.UpdateSpecimenCollection("SD-1", null, null));
    }

    [Fact]
    public void Analyzer_FlagsDatingAsReview_AndWorksheetPrintsTimes()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SD-2", "serum", "2026-11-09", createdDate: "2026-11-01");
        var panelId = iso.Db.AddPanel("P", "L-SD2", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("SD-2", panelId);
        iso.Db.SaveReaction("SD-2", panelId, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("SD-2", panelId, "2", "0", "0", "0", "2+");
        iso.Db.UpdateSpecimenTransfusion("SD-2", "transfused", "2026-10-25");
        iso.Db.UpdateSpecimenCollection("SD-2", "2026-11-01 08:00", "2026-11-01 09:00");

        var previous = (AppSettings.Current.DefaultSpecimenDatingDays, AppSettings.Current.DatingTransfusionLookbackDays);
        try
        {
            AppSettings.Current.DefaultSpecimenDatingDays = 3;
            AppSettings.Current.DatingTransfusionLookbackDays = 90;
            var result = iso.Analyzer.AnalyzeSpecimen("SD-2", updateDb: false);
            Assert.Contains(result.Suggestions, s => s.StartsWith("REVIEW: Specimen dating: expiration 2026-11-09"));

            var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "SD-2");
            Assert.Contains("Collected: 2026-11-01 08:00   Received: 2026-11-01 09:00   Expires: 2026-11-09", report);
        }
        finally
        {
            (AppSettings.Current.DefaultSpecimenDatingDays, AppSettings.Current.DatingTransfusionLookbackDays) = previous;
        }
    }

    [Fact]
    public void Turnaround_UsesRecordedReceivedTime()
    {
        var s = new Specimen
        {
            AccessionNumber = "T", CreatedDate = "2026-11-01", ReceivedAt = "2026-11-01 18:00",
            FinalAntibodies = "anti-E", IdentifiedAt = "2026-11-02 06:00",
        };
        var t = TurnaroundService.Compute(new[] { s }, null, null, 3, new DateTime(2026, 11, 5));
        Assert.Equal(0.5, t.ReceivedToConfirmed.Median);
    }
}
