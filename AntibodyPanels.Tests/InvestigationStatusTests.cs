using AntibodyPanels.Models;
using AntibodyPanels.Services;
using AntibodyPanels.Tests.Infrastructure;

namespace AntibodyPanels.Tests;

[Collection("PersistentDatabase")]
public class InvestigationStatusTests
{
    private static InvestigationStatus StatusOf(IsolatedDatabase iso, string acc) =>
        InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen(acc)!);

    private static int AttachTwoCellPanel(IsolatedDatabase iso, string acc)
    {
        var panelId = iso.Db.AddPanel("Panel A", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        return panelId;
    }

    [Fact]
    public void NewSpecimen_IsAwaitingPanel_WithOnlyReceivedDone()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-1", "serum", null);
        var st = StatusOf(iso, "ST-1");
        Assert.Equal(InvestigationStatusService.AwaitingPanel, st.Label);
        Assert.Contains("Attach a panel", st.NextAction);
        Assert.Equal(new[] { true, false, false, false, false }, st.Steps.Select(s => s.Done));
    }

    [Fact]
    public void PartlyGraded_IsTestingInProgress_AndNamesPanelCounts()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-2", "serum", null);
        var panelId = AttachTwoCellPanel(iso, "ST-2");
        iso.Db.SaveReaction("ST-2", panelId, "1", "0", "0", "2+", "NT");
        var st = StatusOf(iso, "ST-2");
        Assert.Equal(InvestigationStatusService.TestingInProgress, st.Label);
        Assert.Contains("Panel A (1/2)", st.NextAction);
        Assert.True(st.Steps[1].Done);
        Assert.False(st.Steps[2].Done);
    }

    [Fact]
    public void FullyGraded_NotAnalyzed_IsAnalysisNeeded_ThenAwaitingIdAfterAnalysis_ThenStaleAgain()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-3", "serum", null);
        var panelId = AttachTwoCellPanel(iso, "ST-3");
        iso.Db.SaveReaction("ST-3", panelId, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("ST-3", panelId, "2", "0", "0", "0", "2+");
        Assert.Equal(InvestigationStatusService.AnalysisNeeded, StatusOf(iso, "ST-3").Label);
        Assert.Equal("Run analysis.", StatusOf(iso, "ST-3").NextAction);

        iso.Analyzer.AnalyzeSpecimen("ST-3", updateDb: true);
        var st = StatusOf(iso, "ST-3");
        Assert.Equal(InvestigationStatusService.AwaitingIdentification, st.Label);
        Assert.True(st.Steps[3].Done);

        Thread.Sleep(1100); // timestamps have one-second resolution
        iso.Db.SaveReaction("ST-3", panelId, "2", "0", "0", "1+", "NT");
        var stale = StatusOf(iso, "ST-3");
        Assert.Equal(InvestigationStatusService.AnalysisNeeded, stale.Label);
        Assert.Contains("changed since the last analysis", stale.NextAction);
    }

    [Fact]
    public void ConfirmedId_IsCompleted_AllStepsDone()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-4", "serum", null);
        var panelId = AttachTwoCellPanel(iso, "ST-4");
        iso.Db.SaveReaction("ST-4", panelId, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("ST-4", panelId, "2", "0", "0", "0", "2+");
        iso.Analyzer.AnalyzeSpecimen("ST-4", updateDb: true);
        iso.Db.SetSpecimenFinalCall("ST-4", "anti-E", null, "DP");
        var st = StatusOf(iso, "ST-4");
        Assert.Equal(InvestigationStatusService.Completed, st.Label);
        Assert.All(st.Steps, s => Assert.True(s.Done));
        Assert.Equal(InvestigationStatusService.Completed, st.Summary);
    }

    [Fact]
    public void ClearedId_IsReopened_ThenAmendedWhenConfirmedAgain()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-5", "serum", null);
        iso.Db.SetSpecimenFinalCall("ST-5", "anti-E", null, "DP");
        iso.Db.ClearSpecimenFinalCall("ST-5", "wrong antigen column");

        var reopened = StatusOf(iso, "ST-5");
        Assert.Equal(InvestigationStatusService.Reopened, reopened.Label);
        Assert.Contains("was cleared", reopened.NextAction);
        Assert.Contains("awaiting panel", reopened.NextAction);

        iso.Db.SetSpecimenFinalCall("ST-5", "anti-c", null, "DP");
        var amended = StatusOf(iso, "ST-5");
        Assert.Equal(InvestigationStatusService.Amended, amended.Label);
        Assert.Contains("cleared 1 time(s)", amended.NextAction);
    }

    [Fact]
    public void InactiveWithoutId_IsInactiveNotCompleted()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-6", "serum", null);
        iso.Db.SetSpecimenActive("ST-6", false);
        var st = StatusOf(iso, "ST-6");
        Assert.Equal(InvestigationStatusService.InactiveIncomplete, st.Label);
        Assert.Contains("Reactivate", st.NextAction);
    }

    [Fact]
    public void Worksheet_PrintsInvestigationStatus()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-7", "serum", null);
        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "ST-7");
        Assert.Contains("Investigation status: Awaiting panel — Next: Attach a panel", report);
    }

    [Fact]
    public void Worklist_StillReportsIncompletePanelsThroughSharedCheck()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ST-8", "serum", null);
        var panelId = AttachTwoCellPanel(iso, "ST-8");
        iso.Db.SaveReaction("ST-8", panelId, "1", "0", "0", "2+", "NT");
        var item = Assert.Single(iso.Db.GetWorklistItems(),
            i => i.AccessionNumber == "ST-8" && i.Kind == WorklistKind.IncompleteReactions);
        Assert.Equal("Reactions not finished: Panel A (1/2)", item.Detail);
    }
}
