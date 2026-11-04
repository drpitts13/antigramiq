using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PregnancyContextTests
{
    private static Specimen S(string pregnancy, string? end = null, string? rhig = null, string? exp = "2026-11-09") => new()
    {
        AccessionNumber = "PG",
        CreatedDate = "2026-11-01",
        CollectedAt = "2026-11-01 08:00",
        ExpirationDate = exp,
        PregnancyStatus = pregnancy,
        PregnancyEndDate = end,
        RhigDate = rhig,
    };

    // ── Dating trigger ──────────────────────────────────────────────────

    [Fact]
    public void Pregnant_TriggersDating()
    {
        var note = SpecimenDating.Check(S(PregnancyStatus.Pregnant), 3, 90);
        Assert.NotNull(note);
        Assert.Contains("current pregnancy", note);
        Assert.Contains("2026-11-04", note);
    }

    [Fact]
    public void RecentPregnancy_WithinLookback_Triggers_OldOneDoesNot()
    {
        Assert.Contains("pregnancy ended 2026-09-15",
            SpecimenDating.Check(S(PregnancyStatus.Recent, end: "2026-09-15"), 3, 90));
        Assert.Null(SpecimenDating.Check(S(PregnancyStatus.Recent, end: "2026-01-15"), 3, 90));
    }

    [Fact]
    public void NotPregnantOrUnknown_DoesNotTrigger()
    {
        Assert.Null(SpecimenDating.Check(S(PregnancyStatus.NotPregnant), 3, 90));
        Assert.Null(SpecimenDating.Check(S(PregnancyStatus.Unknown), 3, 90));
    }

    [Fact]
    public void TransfusionAndPregnancy_AreBothNamed()
    {
        var s = S(PregnancyStatus.Pregnant);
        s.TransfusionStatus = TransfusionStatus.Transfused;
        s.LastTransfusionDate = "2026-10-20";
        Assert.Contains("last transfusion 2026-10-20 and current pregnancy", SpecimenDating.Check(s, 3, 90));
    }

    // ── Passive anti-D ──────────────────────────────────────────────────

    [Fact]
    public void PassiveAntiD_OnlyWhenAntiDAndRhigWithinWindow()
    {
        Assert.Contains("Passive anti-D from RhIG is possible",
            SpecimenDating.PassiveAntiD(S(PregnancyStatus.Pregnant, rhig: "2026-09-20"), true, 84));
        Assert.Null(SpecimenDating.PassiveAntiD(S(PregnancyStatus.Pregnant, rhig: "2026-09-20"), false, 84));
        Assert.Null(SpecimenDating.PassiveAntiD(S(PregnancyStatus.Pregnant, rhig: "2026-05-01"), true, 84));
        Assert.Null(SpecimenDating.PassiveAntiD(S(PregnancyStatus.Pregnant), true, 84));
        Assert.Null(SpecimenDating.PassiveAntiD(S(PregnancyStatus.Pregnant, rhig: "2026-11-05"), true, 84)); // after collection
    }

    [Fact]
    public void Analyzer_ConfirmedAntiDAfterRhig_IsReview()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("PG-1", "serum", "2026-11-04", createdDate: "2026-11-01");
        var panelId = iso.Db.AddPanel("P", "L-PG1", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("PG-1", panelId);
        iso.Db.SaveReaction("PG-1", panelId, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("PG-1", panelId, "2", "0", "0", "0", "2+");
        iso.Db.UpdateSpecimenCollection("PG-1", "2026-11-01 08:00", null);
        iso.Db.UpdateSpecimenPregnancy("PG-1", "pregnant", null, "2026-10-15");
        iso.Db.SetSpecimenFinalCall("PG-1", "anti-D", null, "DP");

        var result = iso.Analyzer.AnalyzeSpecimen("PG-1", updateDb: false);
        Assert.Contains(result.Suggestions, s => s.StartsWith("REVIEW: Anti-D is suspected or confirmed") && s.Contains("2026-10-15"));
    }

    // ── Persistence ─────────────────────────────────────────────────────

    [Fact]
    public void UpdatePregnancy_PersistsAudits_AndValidates()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("PG-2", "serum", null);
        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenPregnancy("PG-2", "recent", null, null));
        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenPregnancy("PG-2", "pregnant", null,
            DateTime.Today.AddDays(2).ToString("yyyy-MM-dd")));

        iso.Db.UpdateSpecimenPregnancy("PG-2", "recent", "2026-10-01", "2026-10-02");
        var s = iso.Db.GetSpecimen("PG-2")!;
        Assert.Equal(PregnancyStatus.Recent, s.PregnancyStatus);
        Assert.Equal("Recently pregnant (ended 2026-10-01); RhIG 2026-10-02", s.PregnancyDisplay);
        Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "PG-2"), e => e.Action == "update_pregnancy");

        iso.Db.UpdateSpecimenPregnancy("PG-2", "pregnant", "2026-10-01", null); // end date dropped
        Assert.Null(iso.Db.GetSpecimen("PG-2")!.PregnancyEndDate);
        Assert.Contains(InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("PG-2")!),
            e => e.What == "Pregnancy record set: Recently pregnant, ended 2026-10-01; RhIG 2026-10-02");
    }

    [Fact]
    public void NewSpecimen_DefaultsUnknown_AndWorksheetShowsPregnancy()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("PG-3", "serum", null);
        Assert.Equal(PregnancyStatus.Unknown, iso.Db.GetSpecimen("PG-3")!.PregnancyStatus);
        iso.Db.UpdateSpecimenPregnancy("PG-3", "pregnant", null, null);
        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "PG-3");
        Assert.Contains("Pregnancy: Pregnant", report);
    }
}
