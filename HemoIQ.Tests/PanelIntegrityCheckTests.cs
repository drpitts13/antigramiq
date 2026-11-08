using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

public class PanelIntegrityCheckTests
{
    private static void TypeK(IsolatedDatabase iso, int panelId, params string[] cellNumbers)
    {
        foreach (var cell in iso.Db.GetPanelCells(panelId).Where(c => cellNumbers.Contains(c.CellNumber)))
            iso.Db.UpdatePanelCellAntigen(cell.Id, "k", "+");
    }

    [Fact]
    public void NewActivePanel_IsUntyped_UntilTypingEntered_AcIgnored()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Fresh", "F-1", "V", 3, null, includeAc: true, isActive: true);

        var item = Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.UntypedPanel);
        Assert.Equal(id, item.PanelId);
        Assert.Equal("Untyped cells", item.KindLabel);
        Assert.Contains("no antigen is typed positive on any of its 3 cells", item.Detail);

        TypeK(iso, id, "1", "2");
        item = Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.UntypedPanel);
        Assert.Contains("cell(s) 3 have no antigen typed positive", item.Detail);

        TypeK(iso, id, "3");
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.UntypedPanel);
    }

    [Fact]
    public void InactivePanel_IsNotListedAsUntyped()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddPanel("Old", "O-1", "V", 2, null, false, isActive: false);
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.UntypedPanel);
    }

    [Fact]
    public void Analysis_OnUntypedPanel_AddsReviewNote()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("UT-1", "serum", null);
        var id = iso.Db.AddPanel("Blank", "B-1", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("UT-1", id);
        iso.Db.SaveReaction("UT-1", id, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("UT-1", id, "2", "0", "0", "0", "2+");

        var result = iso.Analyzer.AnalyzeSpecimen("UT-1", updateDb: false);
        Assert.Contains(result.TransfusionRequirementNotes, n =>
            n.StartsWith("Blank: Lot B-1: no antigen is typed positive") && n.Contains("re-analyze"));

        TypeK(iso, id, "1", "2");
        result = iso.Analyzer.AnalyzeSpecimen("UT-1", updateDb: false);
        Assert.DoesNotContain(result.TransfusionRequirementNotes, n => n.Contains("no antigen is typed positive"));
    }

    [Fact]
    public void DuplicateLots_MatchIgnoringCaseAndSpaces_BlankVendorMatchesAny()
    {
        using var iso = new IsolatedDatabase();
        var a = iso.Db.AddPanel("A", "ab-1", "Immucor", 1, null, false, isActive: true);
        var b = iso.Db.AddPanel("B", " AB-1 ", "immucor", 1, null, false, isActive: false);
        var c = iso.Db.AddPanel("C", "AB-1", null, 1, null, false, isActive: false);
        iso.Db.AddPanel("D", "AB-1", "Ortho", 1, null, false, isActive: true);

        var groups = PanelIntegrityCheck.DuplicateLotGroups(iso.Db.GetAllPanels());
        var group = Assert.Single(groups);
        Assert.Equal(new[] { a, b, c }, group.Select(p => p.PanelId));

        var item = Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.DuplicateLot);
        Assert.Equal("Lot ab-1", item.Title);
        Assert.Equal(a, item.PanelId);
        Assert.Contains("3 panels share this lot", item.Detail);
        Assert.Contains($"#{b} 'B' (immucor), inactive", item.Detail);
    }

    [Fact]
    public void DuplicateLots_AllInactive_OrDifferentLots_AreNotListed()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddPanel("A", "X-1", "V", 1, null, false, isActive: false);
        iso.Db.AddPanel("B", "X-1", "V", 1, null, false, isActive: false);
        iso.Db.AddPanel("C", "Y-1", "V", 1, null, false, isActive: true);
        iso.Db.AddPanel("D", "", "V", 1, null, false, isActive: true);
        iso.Db.AddPanel("E", null, "V", 1, null, false, isActive: true);
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.DuplicateLot);
    }

    [Fact]
    public void DuplicatesOf_ExcludesThePanelBeingEdited()
    {
        using var iso = new IsolatedDatabase();
        var a = iso.Db.AddPanel("A", "Z-9", "V", 1, null, false);
        Assert.Empty(PanelIntegrityCheck.DuplicatesOf(iso.Db.GetAllPanels(), "V", "z-9", a));
        Assert.Single(PanelIntegrityCheck.DuplicatesOf(iso.Db.GetAllPanels(), "V", "z-9"));
        Assert.Empty(PanelIntegrityCheck.DuplicatesOf(iso.Db.GetAllPanels(), "V", "  "));
    }
}

public class PanelTypingSummaryTests
{
    [Fact]
    public void Summary_CountsFullyTyped_ListsPartialAndUntyped_IgnoresAc()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Sum", "SM-1", "V", 3, null, includeAc: true);
        var cells = iso.Db.GetPanelCells(id);
        foreach (var c in cells.Where(c => c.CellNumber is "1" or "2"))
        {
            iso.Db.UpdatePanelCellAntigen(c.Id, "k", "+");
            iso.Db.UpdatePanelCellAntigen(c.Id, "K", "-");
        }
        iso.Db.UpdatePanelCellAntigen(cells[2].Id, "K", "-");
        iso.Db.UpdatePanelCellAntigen(cells[0].Id, "Fya", "+");

        var s = PanelIntegrityCheck.Summarize(iso.Db.GetPanelCells(id));
        Assert.Equal(3, s.Cells);
        Assert.Equal(1, s.FullyTyped);
        Assert.Equal(new[] { "k", "Fya" }, s.Partial);
        Assert.Equal(new[] { "3" }, s.Untyped);
        Assert.True(s.HasIssues);
        Assert.Equal("1 antigen · k, Fya partial · cell 3 untyped", s.Display);
    }

    [Fact]
    public void Summary_FreshPanelIsNotTyped_CompletePanelHasNoIssues()
    {
        using var iso = new IsolatedDatabase();
        var fresh = iso.Db.AddPanel("Fresh", "FR-1", "V", 2, null, false);
        Assert.Equal("Not typed", PanelIntegrityCheck.Summarize(iso.Db.GetPanelCells(fresh)).Display);

        var full = iso.Db.AddPanel("Full", "FU-1", "V", 2, null, false, startTypedNegative: true);
        foreach (var c in iso.Db.GetPanelCells(full)) iso.Db.UpdatePanelCellAntigen(c.Id, "k", "+");
        var s = PanelIntegrityCheck.Summarize(iso.Db.GetPanelCells(full));
        Assert.False(s.HasIssues);
        Assert.Equal($"{AntigenConstants.Antigens.Count} antigens", s.Display);
    }

    [Fact]
    public void PanelSummaryReport_ShowsTypingAndDataQualityFindings_AllPanelsShowsTyping()
    {
        using var iso = new IsolatedDatabase();
        var a = iso.Db.AddPanel("Lot A", "DUP-7", "V", 2, null, false);
        iso.Db.AddPanel("Lot A copy", "dup-7", "V", 2, null, false, startTypedNegative: true);
        iso.Db.UpdatePanelCellAntigen(iso.Db.GetPanelCells(a)[0].Id, "K", "+");

        var report = iso.Reports.GeneratePreviewText(ReportType.PanelSummary, panelId: a);
        Assert.Contains("Typing:     0 antigens · K partial · cell 2 untyped", report);
        Assert.Contains("Data quality (review before use):", report);
        Assert.Contains("cell(s) 2 have no antigen typed positive", report);
        Assert.Contains("Typed on some cells but NT on others: K.", report);
        Assert.Contains("Lot also stored as #", report);

        var all = iso.Reports.GeneratePreviewText(ReportType.AllPanels);
        Assert.Contains("0 antigens · K partial · cell 2 untyped", all);
        Assert.Contains($"{AntigenConstants.Antigens.Count} antigens · cells 1, 2 untyped", all);
    }

    [Fact]
    public void PanelSummaryReport_CleanPanel_HasNoDataQualitySection()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Clean", "CL-1", "V", 1, null, false, startTypedNegative: true);
        iso.Db.UpdatePanelCellAntigen(iso.Db.GetPanelCells(id)[0].Id, "k", "+");
        var report = iso.Reports.GeneratePreviewText(ReportType.PanelSummary, panelId: id);
        Assert.DoesNotContain("Data quality", report);
    }
}
