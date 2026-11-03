using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class ReportOutputAuditTests
{
    private static string TempFile(string ext) =>
        Path.Combine(TestDataRoot.Path, $"report_{Guid.NewGuid():N}.{ext}");

    private static List<HemoIQ.Models.AuditEvent> ReportEvents(IsolatedDatabase iso, string type, string id) =>
        iso.Db.GetAuditEventsForEntity(type, id).Where(e => e.Action == "report_output").ToList();

    private static string Prop(string json, string name) =>
        JsonDocument.Parse(json).RootElement.GetProperty(name).ToString();

    [Fact]
    public void PdfExport_OfSpecimenReport_IsAuditedOnTheSpecimen_WithIdAtIssue()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-1", "serum", null);
        iso.Db.SetSpecimenFinalCall("RO-1", "anti-E", null, "DP");
        var path = TempFile("pdf");
        new ReportService(iso.Db).ExportToPdf(ReportType.ClinicalIdentification, path, "RO-1");

        var ev = Assert.Single(ReportEvents(iso, "specimen", "RO-1"));
        Assert.Equal("ClinicalIdentification", Prop(ev.AfterJson!, "reportType"));
        Assert.Equal("pdf", Prop(ev.AfterJson!, "output"));
        Assert.Equal(Path.GetFileName(path), Prop(ev.AfterJson!, "file"));
        Assert.Equal("anti-E", Prop(ev.AfterJson!, "finalAntibodies"));
        Assert.Equal(64, Prop(ev.AfterJson!, "sha256").Length);
    }

    [Fact]
    public void CsvExport_FingerprintMatchesTheWrittenFile()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-2", "serum", null);
        var path = TempFile("csv");
        new ReportService(iso.Db).ExportToCsv(ReportType.SpecimenSummary, path, "RO-2");

        var ev = Assert.Single(ReportEvents(iso, "specimen", "RO-2"));
        var expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(expected, Prop(ev.AfterJson!, "sha256"));
    }

    [Fact]
    public void Print_IsAudited_WithFingerprintOfPrintedText()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-3", "serum", null);
        new ReportService(iso.Db).RecordPrint(ReportType.ClinicalIdentification, "RO-3", null, "printed body");
        var ev = Assert.Single(ReportEvents(iso, "specimen", "RO-3"));
        Assert.Equal("print", Prop(ev.AfterJson!, "output"));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("printed body"))),
            Prop(ev.AfterJson!, "sha256"));
    }

    [Fact]
    public void PanelAndGlobalReports_AttachToPanelOrReport()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Panel R", "LR", "V", 2, null, false);
        var reports = new ReportService(iso.Db);
        reports.ExportToCsv(ReportType.PanelSummary, TempFile("csv"), panelId: panelId);
        reports.ExportToCsv(ReportType.AllSpecimens, TempFile("csv"));

        Assert.Single(ReportEvents(iso, "panel", panelId.ToString()));
        Assert.Single(ReportEvents(iso, "report", "AllSpecimens"));
    }

    [Fact]
    public void FailedExport_WritesNoAudit()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-4", "serum", null);
        var badPath = Path.Combine(TestDataRoot.Path, "no_such_dir", "x.pdf");
        Assert.ThrowsAny<Exception>(() =>
            new ReportService(iso.Db).ExportToPdf(ReportType.ClinicalIdentification, badPath, "RO-4"));
        Assert.Empty(ReportEvents(iso, "specimen", "RO-4"));
    }

    [Fact]
    public void Timeline_ShowsReportIssue()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-5", "serum", null);
        iso.Db.SetSpecimenFinalCall("RO-5", "anti-K", null, "DP");
        new ReportService(iso.Db).ExportToPdf(ReportType.ClinicalIdentification, TempFile("pdf"), "RO-5");
        var whats = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("RO-5")!).Select(e => e.What).ToList();
        Assert.Contains(whats, w => w.StartsWith("Report exported to PDF: Clinical Identification") &&
                                    w.Contains("ID at issue: anti-K") && w.Contains("fingerprint "));
    }

    [Fact]
    public void Amended_AfterIssuedReport_AsksToReissue_UntilANewReportIsIssued()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-6", "serum", null);
        var reports = new ReportService(iso.Db);
        iso.Db.SetSpecimenFinalCall("RO-6", "anti-E", null, "DP");
        reports.RecordPrint(ReportType.ClinicalIdentification, "RO-6", null, "v1");
        iso.Db.ClearSpecimenFinalCall("RO-6", "wrong column");
        iso.Db.SetSpecimenFinalCall("RO-6", "anti-c", null, "DP");

        var st = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("RO-6")!, requireReview: false);
        Assert.Equal(InvestigationStatusService.Amended, st.Label);
        Assert.StartsWith("Reissue the Clinical Identification report", st.NextAction);

        reports.RecordPrint(ReportType.ClinicalIdentification, "RO-6", null, "v2");
        var after = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("RO-6")!, requireReview: false);
        Assert.Equal(InvestigationStatusService.Amended, after.Label);
        Assert.DoesNotContain("Reissue", after.NextAction);
    }

    [Fact]
    public void Amended_WithoutAnyIssuedReport_DoesNotAskToReissue()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("RO-7", "serum", null);
        iso.Db.SetSpecimenFinalCall("RO-7", "anti-E", null, "DP");
        iso.Db.ClearSpecimenFinalCall("RO-7", "typo");
        iso.Db.SetSpecimenFinalCall("RO-7", "anti-E", null, "DP");
        var st = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("RO-7")!, requireReview: false);
        Assert.DoesNotContain("Reissue", st.NextAction);
    }
}
