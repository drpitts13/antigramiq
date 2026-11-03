using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class InvestigationTimelineTests
{
    private static List<TimelineEvent> TimelineOf(IsolatedDatabase iso, string acc) =>
        InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen(acc)!);

    [Fact]
    public void NewSpecimen_ShowsOnlyReceived()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TL-1", "serum", null, createdDate: "2026-11-01");
        var e = Assert.Single(TimelineOf(iso, "TL-1"));
        Assert.Equal("Specimen received", e.What);
        Assert.Equal("2026-11-01", e.WhenDisplay);
    }

    [Fact]
    public void FullWorkup_IsChronological_AndNamesEachStep()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TL-2", "serum", null, createdDate: "2026-01-01");
        iso.Db.UpdateSpecimenMrn("TL-2", "MRN-T");
        iso.Db.SavePatient(new Patient { Mrn = "MRN-T", Name = "Doe" });
        var panelId = iso.Db.AddPanel("Panel A", "LOT9", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("TL-2", panelId);
        iso.Db.SaveReaction("TL-2", panelId, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("TL-2", panelId, "2", "0", "0", "0", "2+");
        iso.Db.SaveReaction("TL-2", panelId, "2", "0", "0", "0", "3+"); // regrade same cell
        iso.Analyzer.AnalyzeSpecimen("TL-2", updateDb: true);
        iso.Db.UpdateSpecimenTransfusion("TL-2", "transfused", "2025-12-20");
        iso.Db.SetSpecimenFinalCall("TL-2", "anti-E", null, "DP");
        iso.Db.ClearSpecimenFinalCall("TL-2", "wrong column");
        iso.Db.SetSpecimenFinalCall("TL-2", "anti-c", null, "DP");
        iso.Db.SetSpecimenSupervisorReview("TL-2", "AB");

        var whats = TimelineOf(iso, "TL-2").Select(e => e.What).ToList();
        Assert.Equal("Specimen received", whats[0]);
        Assert.Contains("Panel attached: Panel A (lot LOT9)", whats);
        Assert.Contains(whats, w => w.StartsWith("Panel run started: Panel A"));
        var rx = Assert.Single(whats, w => w.StartsWith("Reactions entered"));
        Assert.Contains("3 save(s), 2 cell(s)", rx);
        Assert.Contains(whats, w => w.StartsWith("Analysis run"));
        Assert.Contains("Transfusion record set: Transfused, last 2025-12-20", whats);
        Assert.Contains("Identification confirmed: anti-E by DP", whats);
        Assert.Contains("Identification cleared: anti-E; reason: wrong column", whats);
        Assert.Contains("Identification confirmed: anti-c by DP", whats);
        Assert.Contains("Supervisor reviewed by AB", whats);
        Assert.Contains("Patient record created for MRN-T", whats);

        // Ordered: confirm anti-E before clear before confirm anti-c before review.
        int I(string w) => whats.IndexOf(w);
        Assert.True(I("Identification confirmed: anti-E by DP") < I("Identification cleared: anti-E; reason: wrong column"));
        Assert.True(I("Identification cleared: anti-E; reason: wrong column") < I("Identification confirmed: anti-c by DP"));
        Assert.True(I("Identification confirmed: anti-c by DP") < I("Supervisor reviewed by AB"));
    }

    [Fact]
    public void TreatedRun_IsAdditionalTesting_AndUnlinkIsRecorded()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TL-3", "serum", null, createdDate: "2026-02-01");
        var panelId = iso.Db.AddPanel("Panel B", null, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("TL-3", panelId);
        iso.Db.AddPanelRun("TL-3", panelId, CellTreatment.Ficin, SerumTreatment.None);
        iso.Db.UnlinkSpecimenPanel("TL-3", panelId);
        iso.Db.UnlinkSpecimenPanel("TL-3", panelId); // second unlink is a no-op

        var whats = TimelineOf(iso, "TL-3").Select(e => e.What).ToList();
        Assert.Contains(whats, w => w.StartsWith("Additional testing started:") && w.Contains("Panel B"));
        Assert.Single(whats, w => w == "Panel removed: Panel B");
    }

    [Fact]
    public void OtherSpecimensEvents_AreExcluded()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TL-4", "serum", null);
        iso.Db.AddSpecimen("TL-5", "serum", null);
        iso.Db.SetSpecimenFinalCall("TL-5", "anti-K", null, "DP");
        Assert.DoesNotContain(TimelineOf(iso, "TL-4"), e => e.What.Contains("anti-K"));
    }

    [Fact]
    public void Worksheet_PrintsTimeline()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TL-6", "serum", null, createdDate: "2026-03-03");
        iso.Db.SetSpecimenFinalCall("TL-6", "anti-E", null, "DP");
        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "TL-6");
        Assert.Contains("INVESTIGATION TIMELINE", report);
        Assert.Contains("2026-03-03  Specimen received", report);
        Assert.Contains("Identification confirmed: anti-E by DP", report);
    }
}
