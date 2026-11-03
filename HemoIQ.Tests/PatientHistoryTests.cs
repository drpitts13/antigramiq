using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PatientHistoryTests
{
    private static void AddReactiveSpecimen(IsolatedDatabase iso, string acc, string? mrn, string created)
    {
        iso.Db.AddSpecimen(acc, "serum", null, createdDate: created);
        iso.Db.UpdateSpecimenMrn(acc, mrn);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction(acc, panelId, "2", "0", "0", "0", "2+");
    }

    [Theory]
    [InlineData(" mrn-001 ", "MRN-001")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeMrn_TrimsAndUppercases(string? input, string? expected) =>
        Assert.Equal(expected, PatientHistory.NormalizeMrn(input));

    [Fact]
    public void ConfirmedPriorAntibody_OnSameMrn_BecomesHistoricalEvidence_WithSource()
    {
        using var iso = new IsolatedDatabase();
        AddReactiveSpecimen(iso, "OLD-1", "MRN-7", "2026-03-01");
        iso.Db.SetSpecimenFinalCall("OLD-1", "anti-Jka", null, "DP");
        AddReactiveSpecimen(iso, "NEW-1", "mrn-7", "2026-11-03");

        var result = iso.Analyzer.AnalyzeSpecimen("NEW-1", updateDb: false);
        var c = Assert.Single(result.PatientTypingConsiderations,
            x => x.Antibody == "anti-Jka" && x.Kind == PatientTypingKind.Historical);
        Assert.Contains("accession OLD-1", c.Explanation);
        Assert.Contains("fall below detection", c.Explanation);
        Assert.Contains(result.Suggestions, s => s.Contains("anti-Jka was confirmed on accession OLD-1"));
    }

    [Fact]
    public void DifferentMrn_BlankMrn_AndUnconfirmedSpecimens_ContributeNothing()
    {
        using var iso = new IsolatedDatabase();
        AddReactiveSpecimen(iso, "OTHER-PT", "MRN-9", "2026-03-01");
        iso.Db.SetSpecimenFinalCall("OTHER-PT", "anti-K", null, "DP");
        AddReactiveSpecimen(iso, "UNCONF", "MRN-8", "2026-04-01"); // no final call
        AddReactiveSpecimen(iso, "NEW-8", "MRN-8", "2026-11-03");
        AddReactiveSpecimen(iso, "NO-MRN", null, "2026-11-03");

        foreach (var acc in new[] { "NEW-8", "NO-MRN" })
        {
            var result = iso.Analyzer.AnalyzeSpecimen(acc, updateDb: false);
            Assert.DoesNotContain(result.PatientTypingConsiderations, x => x.Kind == PatientTypingKind.Historical);
        }
    }

    [Fact]
    public void PriorAntibodyAlreadyTypedInPreviousAntibodies_IsNotDuplicated()
    {
        using var iso = new IsolatedDatabase();
        AddReactiveSpecimen(iso, "OLD-2", "MRN-2", "2026-01-01");
        iso.Db.SetSpecimenFinalCall("OLD-2", "anti-E; anti-K", null, "DP");
        iso.Db.AddSpecimen("NEW-2", "serum", null, previousAntibodies: "anti-E", createdDate: "2026-11-03");
        iso.Db.UpdateSpecimenMrn("NEW-2", "MRN-2");
        var panelId = iso.Db.AddPanel("P", "L-NEW-2", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("NEW-2", panelId);
        iso.Db.SaveReaction("NEW-2", panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction("NEW-2", panelId, "2", "0", "0", "0", "2+");

        var result = iso.Analyzer.AnalyzeSpecimen("NEW-2", updateDb: false);
        Assert.Single(result.PatientTypingConsiderations, x => x.Antibody == "anti-E");
        Assert.Single(result.PatientTypingConsiderations,
            x => x.Antibody == "anti-K" && x.Explanation.Contains("OLD-2"));
    }

    [Fact]
    public void ConfirmedAntibodies_CiteEarliestConfirmation_AndSkipNonAntibodyText()
    {
        var others = new[]
        {
            new Specimen { AccessionNumber = "B", CreatedDate = "2026-05-01", FinalAntibodies = "anti-K; warm autoantibody" },
            new Specimen { AccessionNumber = "A", CreatedDate = "2026-02-01", FinalAntibodies = "anti-K" },
        };
        var list = PatientHistory.ConfirmedAntibodies(others);
        var k = Assert.Single(list);
        Assert.Equal("A", k.Accession);
    }

    [Fact]
    public void UpdateMrn_IsAudited_LockedAfterFinalCall_AndLengthChecked()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("MRN-A", "serum", null);
        iso.Db.UpdateSpecimenMrn("MRN-A", "abc123");
        Assert.Equal("ABC123", iso.Db.GetSpecimen("MRN-A")!.Mrn);
        Assert.Single(iso.Db.GetAuditEvents(), e => e.Action == "update_mrn" && e.EntityId == "MRN-A");

        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenMrn("MRN-A", new string('X', 65)));
        iso.Db.SetSpecimenFinalCall("MRN-A", "anti-E", null, "DP");
        Assert.Throws<RecordLockedException>(() => iso.Db.UpdateSpecimenMrn("MRN-A", "OTHER"));
    }

    [Fact]
    public void Reports_ShowMrnAndOtherSpecimens()
    {
        using var iso = new IsolatedDatabase();
        AddReactiveSpecimen(iso, "OLD-R", "MRN-R", "2026-02-01");
        iso.Db.SetSpecimenFinalCall("OLD-R", "anti-Fya", null, "DP");
        AddReactiveSpecimen(iso, "NEW-R", "MRN-R", "2026-11-03");

        var reports = new ReportService(iso.Db);
        var summary = reports.GeneratePreviewText(ReportType.SpecimenSummary, "NEW-R");
        Assert.Contains("MRN:             MRN-R", summary);
        Assert.Contains("OLD-R (2026-02-01): confirmed anti-Fya", summary);
        var worksheet = reports.GeneratePreviewText(ReportType.ClinicalIdentification, "NEW-R");
        Assert.Contains("MRN: MRN-R", worksheet);
        Assert.Contains("OLD-R", worksheet);
    }

    [Fact]
    public void SpecimenFilter_MatchesMrn()
    {
        Assert.True(new Specimen { AccessionNumber = "X", Mrn = "MRN-55" }.MatchesFilter("mrn-55"));
    }
}
