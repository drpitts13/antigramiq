using System.Text.Json;
using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class MultipleAntibodySeparationTests
{
    // (E, K, reactive) per cell. Cells 7–9 are E- K- nonreactive so both antibodies meet 3 + 3.
    private static readonly (string E, string K, bool Reactive)[] Separated =
    {
        ("+", "-", true), ("+", "-", true), ("+", "-", true),
        ("-", "+", true), ("-", "+", true), ("-", "+", true),
        ("-", "-", false), ("-", "-", false), ("-", "-", false),
    };

    // Every reactive K+ cell is also E+: anti-K cannot be separated from anti-E.
    private static readonly (string E, string K, bool Reactive)[] KAlwaysWithE =
    {
        ("+", "-", true), ("+", "-", true), ("+", "+", true),
        ("+", "+", true), ("+", "+", true),
        ("-", "-", false), ("-", "-", false), ("-", "-", false),
        ("-", "-", false),
    };

    private static void Seed(IsolatedDatabase iso, string acc, (string E, string K, bool Reactive)[] cells,
        string? previousAntibodies = null, string? mrn = null)
    {
        iso.Db.AddSpecimen(acc, "serum", null, previousAntibodies: previousAntibodies);
        if (mrn != null) iso.Db.UpdateSpecimenMrn(acc, mrn);
        var panelId = iso.Db.AddPanel("Sep " + acc, "L-" + acc, "V", cells.Length, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        var panelCells = iso.Db.GetPanelCells(panelId);
        for (int i = 0; i < cells.Length; i++)
        {
            iso.Db.UpdatePanelCellAntigen(panelCells[i].Id, "E", cells[i].E);
            iso.Db.UpdatePanelCellAntigen(panelCells[i].Id, "K", cells[i].K);
        }
        var run = iso.Db.GetOrCreateDefaultRun(acc, panelId);
        iso.Db.SaveReactions(run, cells.Select((c, i) =>
            new ReactionInput((i + 1).ToString(), "0", "0", c.Reactive ? "2+" : "0", c.Reactive ? "NT" : "2+")).ToList());
    }

    private static T WithCount<T>(int count, Func<T> body)
    {
        var previous = AppSettings.Current.SeparationCellCount;
        var idPrevious = AppSettings.Current.IdentificationCellCount;
        AppSettings.Current.SeparationCellCount = count;
        AppSettings.Current.IdentificationCellCount = 3;
        try { return body(); }
        finally
        {
            AppSettings.Current.SeparationCellCount = previous;
            AppSettings.Current.IdentificationCellCount = idPrevious;
        }
    }

    private static SeparationEvaluation For(AnalysisResult r, string ab) =>
        Assert.Single(r.SeparationEvaluations, e => e.Antibody == ab);

    [Fact]
    public void SeparatedAntiEAndAntiK_BothMeetCriterion()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-1", Separated);
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-1", updateDb: false));

        Assert.True(result.SuspectedStatistics["anti-E"].MeetsIdentificationRule);
        Assert.True(result.SuspectedStatistics["anti-K"].MeetsIdentificationRule);
        Assert.Equal(3, For(result, "anti-E").IsolatedCount);
        Assert.Equal(3, For(result, "anti-K").IsolatedCount);
        Assert.All(result.SeparationEvaluations, e => Assert.True(e.MeetsCriterion));
        Assert.Contains(result.Suggestions, s => s.Contains("meeting the suggested multiple-antibody criterion"));
    }

    [Fact]
    public void AntiKOnlyOnEPositiveCells_FallsShort_WithSuggestion()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-2", KAlwaysWithE);
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-2", updateDb: false));

        var k = For(result, "anti-K");
        Assert.Equal(0, k.IsolatedCount);
        Assert.False(k.MeetsCriterion);
        Assert.Equal("0 / 1", k.Display);
        Assert.True(For(result, "anti-E").MeetsCriterion);
        Assert.Contains(result.Suggestions, s => s.Contains("Anti-K has 0 of 1 reactive K+ E- cell(s)"));
    }

    [Fact]
    public void RuleExemptingKFromE_MeetsAsRuleExempt()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-3", KAlwaysWithE);
        iso.Db.AddRule("K with E", null, "anti-K", null, false, 1, separationExempt: true, separationExemptFrom: "E");
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-3", updateDb: false));

        var k = For(result, "anti-K");
        Assert.Equal("rule", k.ExemptReason);
        Assert.True(k.MeetsCriterion);
        Assert.Equal("Exempt (rule)", k.Display);
    }

    [Fact]
    public void RuleWithBlankList_ExemptsFromEveryPartner_ButOtherListDoesNot()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-4", KAlwaysWithE);
        var id = iso.Db.AddRule("K exempt", null, "anti-K", null, false, 1, separationExempt: true);
        var all = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-4", updateDb: false));
        Assert.Equal("rule", For(all, "anti-K").ExemptReason);

        iso.Db.UpdateRule(id, "K exempt", null, "anti-K", null, false, 1, separationExempt: true, separationExemptFrom: "c");
        var other = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-4", updateDb: false));
        Assert.False(For(other, "anti-K").MeetsCriterion);
    }

    [Fact]
    public void PatientHistory_ExemptsThatAntibody_Only()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-5", KAlwaysWithE, previousAntibodies: "anti-K");
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-5", updateDb: false));

        var k = For(result, "anti-K");
        Assert.Equal("patient history", k.ExemptReason);
        Assert.Equal("Exempt (history)", k.Display);
        var e = For(result, "anti-E");
        Assert.Null(e.ExemptReason);
        Assert.Equal(2, e.IsolatedCount);
    }

    [Fact]
    public void PriorConfirmedSpecimenForSameMrn_CountsAsHistory()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SEP-6A", "serum", null);
        iso.Db.UpdateSpecimenMrn("SEP-6A", "MRN-SEP6");
        iso.Db.SetSpecimenFinalCall("SEP-6A", "anti-K", null, "TE");
        Seed(iso, "SEP-6", KAlwaysWithE, mrn: "MRN-SEP6");

        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-6", updateDb: false));
        Assert.Equal("patient history", For(result, "anti-K").ExemptReason);
    }

    [Fact]
    public void UntypedPartnerAntigen_DoesNotCountAsNegative()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-7", Separated);
        var cells = iso.Db.GetPanelCells(iso.Db.GetAllSpecimenRuns("SEP-7")[0].PanelId);
        iso.Db.UpdatePanelCellAntigen(cells[0].Id, "K", "NT");
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-7", updateDb: false));

        Assert.Equal(2, For(result, "anti-E").IsolatedCount);
    }

    [Fact]
    public void CountZeroTurnsOff_AndHigherCountNeedsMoreCells()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-8", KAlwaysWithE, previousAntibodies: "anti-K");
        var off = WithCount(0, () => iso.Analyzer.AnalyzeSpecimen("SEP-8", updateDb: false));
        Assert.Empty(off.SeparationEvaluations);

        var three = WithCount(3, () => iso.Analyzer.AnalyzeSpecimen("SEP-8", updateDb: false));
        var e = For(three, "anti-E");
        Assert.Equal(2, e.IsolatedCount);
        Assert.False(e.MeetsCriterion);
        Assert.Contains("Test 1 more E+ K- cell(s)", e.Explanation);
    }

    [Fact]
    public void SingleCalledAntibody_HasNoEvaluation()
    {
        using var iso = new IsolatedDatabase();
        var single = Separated.Select(c => (c.E, "-", c.E == "+")).ToArray();
        Seed(iso, "SEP-9", single);
        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-9", updateDb: false));

        Assert.Empty(result.SeparationEvaluations);
    }

    [Fact]
    public void RuleFieldsPersist_AndTraceRecordsSetting()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddRule("f with e", "f is a cis product of c and e", "anti-f", null, false, 3,
            separationExempt: true, separationExemptFrom: "e; c");
        var rule = Assert.Single(iso.Db.GetAllRules(), r => r.RuleId == id);
        Assert.True(rule.SeparationExempt);
        Assert.Equal("e, c", rule.SeparationExemptFrom);

        iso.Db.UpdateRule(id, "f", null, "anti-f", null, false, 3);
        rule = Assert.Single(iso.Db.GetAllRules(), r => r.RuleId == id);
        Assert.False(rule.SeparationExempt);
        Assert.Null(rule.SeparationExemptFrom);

        var json = WithCount(2, () => AnalysisRuleTrace.Serialize(iso.Db.GetAllRules(), AppSettings.Current));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("SeparationCellCount").GetInt32());
        Assert.Contains("multiple-antibody separation suggests 2",
            WithCount(2, () => AnalysisRuleTrace.Explain(iso.Db.GetAllRules(), AppSettings.Current)));
    }

    [Fact]
    public void Shortfall_RanksUnusedIsolatingCellFirst_AndSuggestsIt()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-11", KAlwaysWithE);
        var vial = iso.Db.AddPanel("Selected cells", "SEL-11", "V", 2, null, false);
        var vialCells = iso.Db.GetPanelCells(vial);
        iso.Db.UpdatePanelCellAntigen(vialCells[0].Id, "E", "+");
        iso.Db.UpdatePanelCellAntigen(vialCells[0].Id, "K", "+");
        iso.Db.UpdatePanelCellAntigen(vialCells[1].Id, "E", "-");
        iso.Db.UpdatePanelCellAntigen(vialCells[1].Id, "K", "+");

        var result = WithCount(1, () => iso.Analyzer.AnalyzeSpecimen("SEP-11", updateDb: false));
        var top = result.SelectedCellRecommendations.First();
        Assert.Equal((vial, "2"), (top.PanelId, top.CellNumber));
        Assert.True(top.FillsSeparation);
        Assert.Contains("is K+ E- and, if reactive, would count toward the multiple-antibody separation criterion for anti-K",
            top.Explanation);
        Assert.DoesNotContain(result.SelectedCellRecommendations, r => r.CellNumber == "1" && r.PanelId == vial && r.FillsSeparation);
        Assert.Contains(result.Suggestions, s => s.Contains("Selected cells cell 2 have the phenotype needed"));
    }

    [Fact]
    public void ClinicalReport_PrintsSeparation_ForSuspectedAndForConfirmedCall()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-12", KAlwaysWithE);
        var open = WithCount(1, () => iso.Reports.GeneratePreviewText(ReportType.ClinicalIdentification, "SEP-12"));
        Assert.Contains("Multiple-antibody separation (suggested criterion, not a rule-out):", open);
        Assert.Contains("Anti-K: 0 / 1 — SHORT.", open);
        Assert.Single(open.Split('\n'), l => l.Contains("Anti-K has 0 of 1"));

        iso.Db.SetSpecimenFinalCall("SEP-12", "anti-E; anti-K", null, "TE");
        var confirmed = WithCount(1, () => iso.Reports.GeneratePreviewText(ReportType.ClinicalIdentification, "SEP-12"));
        Assert.Contains("Multiple-antibody separation for the confirmed call", confirmed);
        Assert.Contains("Anti-E: 2 / 1.", confirmed);
    }

    [Fact]
    public void History_KeepsAntiEAndAntiEApart()
    {
        var others = new[]
        {
            new Specimen { AccessionNumber = "H1", CreatedDate = "2026-01-01", FinalAntibodies = "Anti-E" },
            new Specimen { AccessionNumber = "H2", CreatedDate = "2026-02-01", FinalAntibodies = "anti-e; anti-E" },
        };
        var list = PatientHistory.ConfirmedAntibodies(others);
        Assert.Equal(new[] { "anti-E", "anti-e" }, list.Select(p => p.Antibody));
        Assert.Equal("H1", list[0].Accession);

        var typing = PatientTypingParser.Parse(null, "anti-E, anti-e", null, null, null);
        Assert.Equal(new[] { "anti-E", "anti-e" }, typing.HistoricalAntibodies);
    }

    [Fact]
    public void PriorAntiLowercaseE_DoesNotExemptAntiE()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-13", KAlwaysWithE, previousAntibodies: "anti-e", mrn: "MRN-SEP13");
        iso.Db.AddSpecimen("SEP-13A", "serum", null);
        iso.Db.UpdateSpecimenMrn("SEP-13A", "MRN-SEP13");
        iso.Db.SetSpecimenFinalCall("SEP-13A", "anti-e", null, "TE");

        var result = WithCount(3, () => iso.Analyzer.AnalyzeSpecimen("SEP-13", updateDb: false));
        Assert.Null(For(result, "anti-E").ExemptReason);
        Assert.False(For(result, "anti-E").MeetsCriterion);
    }

    [Fact]
    public void EvaluateFinalCall_ReportsShortfall_ForConfirmPrompt()
    {
        using var iso = new IsolatedDatabase();
        Seed(iso, "SEP-10", KAlwaysWithE);
        var evaluations = WithCount(1, () => iso.Analyzer.EvaluateFinalCall("SEP-10", "anti-E; Anti-K"));

        Assert.Equal(2, evaluations.Count);
        var message = AnalysisViewModel.SeparationShortfallMessage(evaluations);
        Assert.NotNull(message);
        Assert.Contains("Anti-K has 0 of 1", message);
        Assert.DoesNotContain("Anti-E has", message);

        Assert.Empty(WithCount(1, () => iso.Analyzer.EvaluateFinalCall("SEP-10", "anti-E")));
        Assert.Null(AnalysisViewModel.SeparationShortfallMessage(
            WithCount(1, () => iso.Analyzer.EvaluateFinalCall("SEP-10", "anti-E; anti-K; ACS"))
                .Where(e => e.Antibody == "anti-E")));
    }
}

public class SuggestionPaneTests
{
    [Fact]
    public void CollapsedPane_ShowsFirstThreeInOrder_ExpandedShowsAll()
    {
        var all = new[] { "WARNING: a", "REVIEW: b", "c", "d", "e" };
        Assert.Equal(new[] { "WARNING: a", "REVIEW: b", "c" },
            HemoIQ.ViewModels.AnalysisViewModel.VisibleSuggestions(all, expanded: false));
        Assert.Equal(all, HemoIQ.ViewModels.AnalysisViewModel.VisibleSuggestions(all, expanded: true));
        Assert.Equal(new[] { "x" }, HemoIQ.ViewModels.AnalysisViewModel.VisibleSuggestions(new[] { "x" }, expanded: false));
    }
}
