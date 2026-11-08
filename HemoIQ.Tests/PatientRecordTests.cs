using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PatientRecordTests
{
    // ── Persistence and audit ──────────────────────────────────────────

    [Fact]
    public void SavePatient_CreatesThenUpdates_WithAuditBeforeAndAfter()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.SavePatient(new Patient { Mrn = " mrn-1 ", Name = "Doe, Pat", DateOfBirth = "1980-02-03" });
        var p = iso.Db.GetPatient("MRN-1")!;
        Assert.Equal("Doe, Pat", p.Name);
        Assert.Equal("1980-02-03", p.DateOfBirth);
        Assert.Equal("Doe, Pat (MRN-1), DOB 1980-02-03", p.Display);

        iso.Db.SavePatient(new Patient { Mrn = "MRN-1", Name = "Doe, Pat", DateOfBirth = "1980-02-03",
            TransfusionRequirements = "K-negative" });
        Assert.Equal("K-negative", iso.Db.GetPatient("mrn-1")!.TransfusionRequirements);

        var events = iso.Db.GetAuditEvents().Where(e => e.EntityId == "MRN-1").ToList();
        Assert.Contains(events, e => e.Action == "add_patient" && e.BeforeJson == null);
        var upd = Assert.Single(events, e => e.Action == "update_patient");
        Assert.Contains("K-negative", upd.AfterJson);
        Assert.DoesNotContain("K-negative", upd.BeforeJson);
    }

    [Fact]
    public void SavePatient_Unchanged_WritesNoSecondAudit()
    {
        using var iso = new IsolatedDatabase();
        var p = new Patient { Mrn = "MRN-2", Name = "A" };
        iso.Db.SavePatient(p);
        iso.Db.SavePatient(p);
        Assert.Single(iso.Db.GetAuditEvents(), e => e.EntityId == "MRN-2");
    }

    [Fact]
    public void SavePatient_RejectsMissingMrn_FutureAndInvalidDob()
    {
        using var iso = new IsolatedDatabase();
        Assert.Throws<ArgumentException>(() => iso.Db.SavePatient(new Patient { Mrn = " " }));
        Assert.Throws<ArgumentException>(() => iso.Db.SavePatient(new Patient
        { Mrn = "M", DateOfBirth = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd") }));
        Assert.Throws<ArgumentException>(() => iso.Db.SavePatient(new Patient { Mrn = "M", DateOfBirth = "soon" }));
        Assert.Null(iso.Db.GetPatient("M"));
    }

    [Fact]
    public void GetPatient_NoMrnOrNoRecord_ReturnsNull()
    {
        using var iso = new IsolatedDatabase();
        Assert.Null(iso.Db.GetPatient(null));
        Assert.Null(iso.Db.GetPatient("NONE"));
    }

    // ── Requirement matching ───────────────────────────────────────────

    [Theory]
    [InlineData("K-negative units", "K", true)]
    [InlineData("K neg", "K", true)]
    [InlineData("K−", "K", true)]
    [InlineData("Fy(a-) and Jk(b−)", "Fya", true)]
    [InlineData("Fy(a-) and Jk(b−)", "Jkb", true)]
    [InlineData("Fya negative", "Fya", true)]
    [InlineData("c-negative", "C", false)]   // case matters: c is not C
    [InlineData("E-negative", "e", false)]
    [InlineData("CMV-negative, irradiated", "C", false)]
    [InlineData("k-negative", "K", false)]
    [InlineData(null, "K", false)]
    public void MentionsAntigenNegative_MatchesRequirementText(string? text, string antigen, bool expected) =>
        Assert.Equal(expected, TransfusionRequirementCheck.MentionsAntigenNegative(text, antigen));

    [Fact]
    public void MissingAntigenNegative_NamesOnlyUnlistedKnownAntibodies()
    {
        var patient = new Patient { Mrn = "M", AntigenNegative = "K" };
        var notes = TransfusionRequirementCheck.MissingAntigenNegative(
            new[] { "anti-K", "anti-Jka", "warm autoantibody", "anti-Jka" }, patient, "M");
        var note = Assert.Single(notes);
        Assert.Contains("anti-Jka", note);
        Assert.Contains("Jka-negative", note);
        Assert.Contains("Not an order", note);
    }

    [Fact]
    public void MissingAntigenNegative_NoPatientRecord_SaysSo_AndNoMrnIsSilent()
    {
        var notes = TransfusionRequirementCheck.MissingAntigenNegative(new[] { "anti-E" }, null, "M");
        Assert.Contains("no patient record exists", Assert.Single(notes));
        Assert.Empty(TransfusionRequirementCheck.MissingAntigenNegative(new[] { "anti-E" }, null, null));
    }

    // ── Analysis and reports ───────────────────────────────────────────

    private static void ReactiveSpecimen(IsolatedDatabase iso, string acc, string mrn, string created)
    {
        iso.Db.AddSpecimen(acc, "serum", null, createdDate: created);
        iso.Db.UpdateSpecimenMrn(acc, mrn);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        // Real cells always type positive for something; untyped cells raise their own review note.
        foreach (var cell in iso.Db.GetPanelCells(panelId))
            iso.Db.UpdatePanelCellAntigen(cell.Id, "k", "+");
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction(acc, panelId, "2", "0", "0", "0", "2+");
    }

    [Fact]
    public void Analyzer_PriorConfirmedAntibodyMissingFromRequirements_IsCriticalReview_ThenClearsWhenRecorded()
    {
        using var iso = new IsolatedDatabase();
        ReactiveSpecimen(iso, "PR-OLD", "MRN-P", "2026-01-01");
        iso.Db.SetSpecimenFinalCall("PR-OLD", "anti-Jka", null, "DP");
        ReactiveSpecimen(iso, "PR-NEW", "MRN-P", "2026-11-03");
        iso.Db.SavePatient(new Patient { Mrn = "MRN-P", TransfusionRequirements = "irradiated" });

        var before = iso.Analyzer.AnalyzeSpecimen("PR-NEW", updateDb: false);
        Assert.Contains(before.Suggestions, s => s.StartsWith("REVIEW:") && s.Contains("Jka-negative"));

        iso.Db.SavePatient(new Patient { Mrn = "MRN-P", TransfusionRequirements = "irradiated", AntigenNegative = "Jk(a-)" });
        var after = iso.Analyzer.AnalyzeSpecimen("PR-NEW", updateDb: false);
        Assert.Empty(after.TransfusionRequirementNotes);
        Assert.DoesNotContain(after.Suggestions, s => s.StartsWith("REVIEW:"));
    }

    [Fact]
    public void Reports_PrintPatientRecord_SeparateFromSpecimen()
    {
        using var iso = new IsolatedDatabase();
        ReactiveSpecimen(iso, "RP-1", "MRN-RP", "2026-11-03");
        iso.Db.SavePatient(new Patient
        {
            Mrn = "MRN-RP", Name = "Roe, Sam", DateOfBirth = "1975-05-06",
            TransfusionRequirements = "E-negative", BloodBankComments = "Warm auto since 2024",
        });
        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "RP-1");
        Assert.Contains("Patient: Roe, Sam (MRN-RP), DOB 1975-05-06", report);
        Assert.Contains("Transfusion requirements: E-negative", report);
        Assert.Contains("Blood bank comments: Warm auto since 2024", report);
    }

    [Fact]
    public void FormatPatient_ExplainsMissingMrnAndMissingRecord()
    {
        Assert.Equal("No MRN recorded.", SpecimensViewModel.FormatPatient(null, null));
        Assert.Contains("Use Edit Patient", SpecimensViewModel.FormatPatient(null, "MRN-X"));
        Assert.Contains("None recorded", SpecimensViewModel.FormatPatient(new Patient { Mrn = "MRN-X" }, "MRN-X"));
    }
}
