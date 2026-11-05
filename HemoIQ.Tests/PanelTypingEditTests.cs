using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PanelTypingEditTests
{
    private static int Panel(IsolatedDatabase iso, string lot)
    {
        var id = iso.Db.AddPanel("Typing", lot, "V", 3, null, false);
        foreach (var c in iso.Db.GetPanelCells(id))
        {
            iso.Db.UpdatePanelCellAntigen(c.Id, "Jka", "+");
            iso.Db.UpdatePanelCellAntigen(c.Id, "K", "-");
        }
        return id;
    }

    private static List<PanelCell> EditJka(IsolatedDatabase iso, int panelId, string cellNumber, string value)
    {
        var cells = iso.Db.GetPanelCells(panelId);
        cells.First(c => c.CellNumber == cellNumber).Antigens["Jka"] = value;
        return cells;
    }

    [Fact]
    public void Describe_ListsOnlyChangedTyping()
    {
        using var iso = new IsolatedDatabase();
        var id = Panel(iso, "T-1");
        var cells = EditJka(iso, id, "2", "-");
        cells.First(c => c.CellNumber == "3").DonorId = "D-77";

        var changes = iso.Db.DescribePanelCellChanges(id, cells);

        Assert.Equal(new[] { "Cell 2 Jka: + → -", "Cell 3 donor: — → D-77" }, changes);
        Assert.Empty(iso.Db.DescribePanelCellChanges(id, iso.Db.GetPanelCells(id)));
    }

    [Fact]
    public void UnusedPanel_EditIsSavedAndAudited_WithoutReason()
    {
        using var iso = new IsolatedDatabase();
        var id = Panel(iso, "T-2");

        var saved = iso.Db.SavePanelCellEdits(id, EditJka(iso, id, "1", "-"));

        Assert.Single(saved);
        Assert.Equal("-", iso.Db.GetPanelCells(id).First(c => c.CellNumber == "1").Antigens["Jka"]);
        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("panel", id.ToString()), e => e.Action == "edit_panel_typing");
        Assert.Contains("Cell 1 Jka", audit.AfterJson);
    }

    [Fact]
    public void NoChange_WritesNoAudit()
    {
        using var iso = new IsolatedDatabase();
        var id = Panel(iso, "T-3");
        Assert.Empty(iso.Db.SavePanelCellEdits(id, iso.Db.GetPanelCells(id)));
        Assert.DoesNotContain(iso.Db.GetAuditEventsForEntity("panel", id.ToString()), e => e.Action == "edit_panel_typing");
    }

    [Fact]
    public void PanelWithHistory_EditWithoutReason_IsRefused_AndTypingUnchanged()
    {
        using var iso = new IsolatedDatabase();
        var id = Panel(iso, "T-4");
        iso.Db.AddSpecimen("PT-1", "serum", null);
        iso.Db.LinkSpecimenPanel("PT-1", id);
        iso.Db.SaveReaction("PT-1", id, "1", "0", "0", "2+", "NT");

        var ex = Assert.Throws<ReasonRequiredException>(() => iso.Db.SavePanelCellEdits(id, EditJka(iso, id, "1", "-")));
        Assert.Contains("holds test history", ex.Message);
        Assert.Contains("Cell 1 Jka: + → -", ex.Message);
        Assert.Equal("+", iso.Db.GetPanelCells(id).First(c => c.CellNumber == "1").Antigens["Jka"]);
    }

    [Fact]
    public void PanelWithHistory_EditWithReason_IsAudited_AndOnLinkedSpecimenTimeline()
    {
        using var iso = new IsolatedDatabase();
        var id = Panel(iso, "T-5");
        iso.Db.AddSpecimen("PT-2", "serum", null);
        iso.Db.LinkSpecimenPanel("PT-2", id);
        iso.Db.SetSpecimenFinalCall("PT-2", "anti-Jka", null, "TE");

        iso.Db.SavePanelCellEdits(id, EditJka(iso, id, "2", "-"), "vendor corrected antigram");

        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("panel", id.ToString()), e => e.Action == "edit_panel_typing");
        Assert.Equal("vendor corrected antigram", audit.Reason);
        Assert.Contains("PT-2", audit.BeforeJson);

        var timeline = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("PT-2")!);
        Assert.Contains(timeline, e => e.What == "Panel Typing (lot T-5) typing edited: 1 change(s); reason: vendor corrected antigram");
    }
}
