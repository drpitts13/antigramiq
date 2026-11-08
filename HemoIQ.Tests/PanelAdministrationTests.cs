using HemoIQ.Models;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PanelAdministrationTests
{
    private readonly PersistentDatabaseFixture _fixture;

    public PanelAdministrationTests(PersistentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void GetAllPanels_ReturnsSeededPanelsSortedByName()
    {
        var names = _fixture.Db.GetAllPanels().Select(p => p.Name).ToList();
        Assert.Equal(names.OrderBy(n => n), names);
    }

    [Fact]
    public void AddPanel_CreatesCellsUntested_NotTypedNegative()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Admin Test Panel", "LOT-99", "Test Vendor", 5, "2030-01-01", includeAc: true);
        var panel = iso.Db.GetPanel(panelId);
        Assert.NotNull(panel);
        Assert.Equal(5, panel!.NumCells);
        Assert.True(panel.IncludeAc);

        var cells = iso.Db.GetPanelCells(panelId);
        Assert.Equal(6, cells.Count);
        foreach (var cell in cells)
            foreach (var ag in AntigenConstants.Antigens)
            {
                Assert.Equal("NT", cell.Antigens[ag]);
                Assert.False(cell.HasTypedAntigen(ag));
            }
    }

    [Fact]
    public void AddPanel_StartTypedNegative_IsForSyntheticData()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Synthetic", "S-1", "V", 2, null, false, startTypedNegative: true);
        Assert.All(iso.Db.GetPanelCells(panelId), c => Assert.True(c.HasTypedAntigen("K") && c.GetAntigen("K") == "-"));
    }

    [Fact]
    public void FillNotTestedNegative_FillsOnlyNtCellsOfOneColumn_SkipsAc_AndSavesTyped()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Fill", "FL-1", "V", 3, null, true);
        var rows = iso.Db.GetPanelCells(panelId).Select(c => new PanelCellRow(c)).ToList();
        rows[0].SetAntigen("K", "+");

        Assert.Equal(2, PanelCellRow.FillNotTestedNegative(rows, "K"));
        Assert.Equal(new[] { "+", "-", "-", "NT" }, rows.Select(r => r.GetEditorValue("K")));
        Assert.Equal("NT", rows[1].GetEditorValue("E"));
        Assert.Equal(0, PanelCellRow.FillNotTestedNegative(rows, "K"));

        foreach (var row in rows) iso.Db.UpdatePanelCell(row.Cell);
        var saved = iso.Db.GetPanelCells(panelId);
        Assert.True(saved[1].HasTypedAntigen("K"));
        Assert.False(saved[1].HasTypedAntigen("E"));
    }

    [Fact]
    public void UpdatePanel_DetailsOnly_KeepsTypingAndSpecialNotes()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Keep", "K-1", "V", 3, null, true, specialNotes: "Cell 2 is Bg(a+)");
        var cells = iso.Db.GetPanelCells(panelId);
        iso.Db.UpdatePanelCellAntigen(cells[0].Id, "K", "+");
        iso.Db.UpdatePanelCellAntigen(cells[1].Id, "E", "-");

        iso.Db.UpdatePanel(panelId, "Keep renamed", "K-1", "V", 3, "2031-01-01", true, specialNotes: "Cell 2 is Bg(a+)");
        var after = iso.Db.GetPanelCells(panelId);
        Assert.Equal(cells.Select(c => c.Id), after.Select(c => c.Id));
        Assert.Equal("+", after[0].GetAntigen("K"));
        Assert.True(after[1].HasTypedAntigen("E"));
        Assert.Equal("Cell 2 is Bg(a+)", iso.Db.GetPanel(panelId)!.SpecialNotes);
    }

    [Fact]
    public void UpdatePanel_Grow_AddsUntestedCells_Shrink_RemovesOnlyUntestedCells()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Size", "Z-1", "V", 3, null, false);
        var first = iso.Db.GetPanelCells(panelId)[0];
        iso.Db.UpdatePanelCellAntigen(first.Id, "K", "+");

        iso.Db.UpdatePanel(panelId, "Size", "Z-1", "V", 5, null, true);
        var grown = iso.Db.GetPanelCells(panelId);
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "AC" }, grown.Select(c => c.CellNumber));
        Assert.Equal("+", grown[0].GetAntigen("K"));
        Assert.False(grown[4].HasTypedAntigen("K"));
        var audit = iso.Db.GetAuditEventsForEntity("panel", panelId.ToString()).Last(e => e.Action == "update_panel");
        Assert.Contains("\"cellsAdded\":[\"4\",\"5\",\"AC\"]", audit.AfterJson);

        iso.Db.AddSpecimen("SZ-1", "serum", null);
        iso.Db.LinkSpecimenPanel("SZ-1", panelId);
        iso.Db.SaveReaction("SZ-1", panelId, "4", "0", "0", "2+", "NT");
        iso.Db.SaveReaction("SZ-1", panelId, "5", "NT", "NT", "NT", "NT");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            iso.Db.UpdatePanel(panelId, "Size", "Z-1", "V", 3, null, false));
        Assert.Contains("Cell(s) 4 have recorded reactions", ex.Message);
        Assert.Equal(6, iso.Db.GetPanelCells(panelId).Count);
        Assert.Equal("Size", iso.Db.GetPanel(panelId)!.Name);

        iso.Db.UpdatePanel(panelId, "Size", "Z-1", "V", 4, null, false);
        Assert.Equal(new[] { "1", "2", "3", "4" }, iso.Db.GetPanelCells(panelId).Select(c => c.CellNumber));
    }

    [Fact]
    public void UpdatePanel_RebuildsCellsWhenCellCountChanges()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Resize Panel", "L1", "V", 3, null, false);
        iso.Db.UpdatePanel(panelId, "Resize Panel", "L1", "V", 5, null, false);
        Assert.Equal(5, iso.Db.GetPanelCells(panelId).Count(c => c.CellNumber != "AC"));
    }

    [Fact]
    public void UpdatePanelCellAntigen_PersistsAntigenProfile()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Antigen Panel", "L1", "V", 1, null, false);
        var cell = iso.Db.GetPanelCells(panelId).Single();
        iso.Db.UpdatePanelCellAntigen(cell.Id, "D", "+");
        iso.Db.UpdatePanelCellAntigen(cell.Id, "E", "-");

        var updated = iso.Db.GetPanelCells(panelId).Single();
        Assert.Equal("+", updated.GetAntigen("D"));
        Assert.Equal("-", updated.GetAntigen("E"));
    }

    [Fact]
    public void CopyPanelCells_CopiesFullAntigenProfile()
    {
        using var iso = new IsolatedDatabase();
        var sourceId = iso.Db.AddPanel("Source", "L1", "V", 2, null, false);
        var targetId = iso.Db.AddPanel("Target", "L2", "V", 2, null, false);
        var sourceCell = iso.Db.GetPanelCells(sourceId).First();
        iso.Db.UpdatePanelCellAntigen(sourceCell.Id, "K", "+");

        iso.Db.CopyPanelCells(sourceId, targetId);
        var copied = iso.Db.GetPanelCells(targetId).First(c => c.CellNumber == sourceCell.CellNumber);
        Assert.Equal("+", copied.GetAntigen("K"));
    }

    [Fact]
    public void DeletePanel_CascadesToCellsAndLinks()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("DEL-PANEL-SPEC", "serum", null);
        var panelId = iso.Db.AddPanel("Delete Me", "L1", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("DEL-PANEL-SPEC", panelId);
        iso.Db.DeletePanel(panelId);

        Assert.Null(iso.Db.GetPanel(panelId));
        Assert.Empty(iso.Db.GetSpecimenPanels("DEL-PANEL-SPEC"));
    }

    [Fact]
    public void SeededOrthoPanel_HasCorrectMetadataAndAcRow()
    {
        var ortho = _fixture.Db.GetAllPanels().First(p => p.LotNumber == "ORT2024A");
        Assert.Equal("Ortho Clinical Diagnostics", ortho.Vendor);
        Assert.True(ortho.IncludeAc);
        Assert.Contains(_fixture.Db.GetPanelCells(ortho.PanelId), c => c.CellNumber == "AC");
    }

    [Fact]
    public void PanelCellRow_ToggleAntigen_CyclesNtPlusHomoMinus()
    {
        var cell = new PanelCell { CellNumber = "1" };
        var row = new PanelCellRow(cell);

        Assert.Equal("NT", row.E);
        row.ToggleAntigen("E");
        Assert.Equal("+", row.E);
        row.ToggleAntigen("E");
        Assert.Equal("++", row.E);
        Assert.True(cell.IsExplicitlyHomozygous("E"));
        Assert.False(cell.HasTypedAntigen("e"));
        row.ToggleAntigen("E");
        Assert.Equal("-", row.E);
        row.ToggleAntigen("E");
        Assert.Equal("NT", row.E);
        Assert.False(cell.HasTypedAntigen("E"));
    }

    [Fact]
    public void PanelCellRow_ToggleAntigen_UnpairedD_SkipsHomozygousMark()
    {
        var cell = new PanelCell { CellNumber = "1" };
        var row = new PanelCellRow(cell);
        row.ToggleAntigen("D");
        Assert.Equal("+", row.D);
        row.ToggleAntigen("D");
        Assert.Equal("-", row.D);
        Assert.False(cell.IsExplicitlyHomozygous("D"));
        row.ToggleAntigen("D");
        Assert.Equal("NT", row.D);
    }

    [Fact]
    public void SavedEditorHomozygous_PersistsAndRulesOutWithoutInventingPartner()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Edit ++", "L", "V", 1, null, false);
        var cell = iso.Db.GetPanelCells(panelId).Single();
        iso.Db.UpdatePanelCellAntigen(cell.Id, "e", AntigenConstants.AntigenNotTested);
        var row = new PanelCellRow(iso.Db.GetPanelCells(panelId).Single());
        row.SetAntigen("E", "+");
        row.ToggleAntigen("E");
        Assert.Equal("++", row.E);
        iso.Db.UpdatePanelCell(row.Cell);

        var saved = iso.Db.GetPanelCells(panelId).Single();
        Assert.Equal("++", saved.GetTypedValue("E"));
        Assert.False(saved.HasTypedAntigen("e"));
        Assert.True(saved.IsHomozygousFor("E"));

        iso.Db.AddSpecimen("ED-E-PP", "serum", null);
        iso.Db.LinkSpecimenPanel("ED-E-PP", panelId);
        iso.Db.SaveReaction("ED-E-PP", panelId, saved.CellNumber, "0", "0", "0", "2+");
        var result = iso.Analyzer.AnalyzeSpecimen("ED-E-PP", updateDb: false);
        Assert.True(result.RuledOut.ContainsKey("anti-E"));
        var ev = result.RuleoutEvaluations.Single(e => e.Antibody == "anti-E");
        Assert.True(ev.MeetsCriteria);
        Assert.Contains(ev.Cells, c => c.IsHomozygous);
    }

    [Fact]
    public void SavedEditorNt_IsNotTypedNegativeAndDoesNotRuleOut()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Edit NT", "L", "V", 1, null, false, startTypedNegative: true);
        var cell = iso.Db.GetPanelCells(panelId).Single();
        var row = new PanelCellRow(cell);
        row.SetAntigen("K", "-");
        row.ToggleAntigen("K");
        Assert.Equal("NT", row.K);
        iso.Db.UpdatePanelCell(row.Cell);

        var saved = iso.Db.GetPanelCells(panelId).Single();
        Assert.False(saved.HasTypedAntigen("K"));
        iso.Db.AddSpecimen("ED-K-NT", "serum", null);
        iso.Db.LinkSpecimenPanel("ED-K-NT", panelId);
        iso.Db.SaveReaction("ED-K-NT", panelId, saved.CellNumber, "0", "0", "0", "2+");
        var result = iso.Analyzer.AnalyzeSpecimen("ED-K-NT", updateDb: false);
        Assert.False(result.RuledOut.ContainsKey("anti-K"));
        Assert.Contains("K", result.UntypedClinicallySignificant);
    }
}
