using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PanelExtraAntigenAuditTests
{
    private static List<AuditEvent> Audits(IsolatedDatabase iso, int panelId, string action) =>
        iso.Db.GetAuditEventsForEntity("panel", panelId.ToString()).Where(e => e.Action == action).ToList();

    private static int PanelWithTypedDoa(IsolatedDatabase iso, string lot)
    {
        var id = iso.Db.AddPanel("Extra", lot, "V", 2, null, false);
        iso.Db.AddPanelExtraAntigen(id, "Doa");
        var cells = iso.Db.GetPanelCells(id);
        iso.Db.UpdatePanelCellAntigen(cells[0].Id, "Doa", "+");
        iso.Db.UpdatePanelCellAntigen(cells[1].Id, "Doa", "-");
        return id;
    }

    [Fact]
    public void AddExtraAntigen_IsAuditedOnce_AndCellsStartUntyped()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Extra", "X-1", "V", 2, null, false);
        iso.Db.AddPanelExtraAntigen(id, "Doa");
        iso.Db.AddPanelExtraAntigen(id, "Doa");

        Assert.Single(Audits(iso, id, "add_extra_antigen"));
        Assert.All(iso.Db.GetPanelCells(id), c => Assert.False(c.HasTypedAntigen("Doa")));
    }

    [Fact]
    public void RemoveTypedExtraAntigen_FromLotWithHistory_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        var id = PanelWithTypedDoa(iso, "X-2");
        iso.Db.AddSpecimen("EX-1", "serum", null);
        iso.Db.LinkSpecimenPanel("EX-1", id);
        iso.Db.SaveReaction("EX-1", id, "1", "0", "0", "2+", "NT");

        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.RemovePanelExtraAntigen(id, "Doa"));
        Assert.Contains("Doa is typed on 2 cell(s)", ex.Message);
        Assert.Contains("Doa", iso.Db.GetPanelExtraAntigens(id));
        Assert.True(iso.Db.GetPanelCells(id)[0].HasTypedAntigen("Doa"));
        Assert.Empty(Audits(iso, id, "remove_extra_antigen"));
    }

    [Fact]
    public void RemoveExtraAntigen_FromUnusedLot_IsAudited_WithRemovedValues()
    {
        using var iso = new IsolatedDatabase();
        var id = PanelWithTypedDoa(iso, "X-3");

        iso.Db.RemovePanelExtraAntigen(id, "Doa");

        Assert.DoesNotContain("Doa", iso.Db.GetPanelExtraAntigens(id));
        var audit = Assert.Single(Audits(iso, id, "remove_extra_antigen"));
        using var doc = System.Text.Json.JsonDocument.Parse(audit.BeforeJson!);
        Assert.Equal("+", doc.RootElement.GetProperty("typed").GetProperty("1").GetString());
    }

    [Fact]
    public void RemoveUntypedExtraAntigen_FromLotWithHistory_IsAllowed()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Extra", "X-4", "V", 2, null, false);
        iso.Db.AddPanelExtraAntigen(id, "Dob");
        iso.Db.AddSpecimen("EX-2", "serum", null);
        iso.Db.LinkSpecimenPanel("EX-2", id);
        iso.Db.SaveReaction("EX-2", id, "1", "0", "0", "2+", "NT");

        iso.Db.RemovePanelExtraAntigen(id, "Dob");

        Assert.DoesNotContain("Dob", iso.Db.GetPanelExtraAntigens(id));
    }

    [Fact]
    public void AntigenOrder_ChangeIsAudited_FirstSaveAndRepeatAreNot()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Order", "X-5", "V", 1, null, false);
        var order = AntigenConstants.Antigens.ToList();
        iso.Db.SetPanelAntigenOrder(id, order);
        iso.Db.SetPanelAntigenOrder(id, order);
        Assert.Empty(Audits(iso, id, "set_antigen_order"));

        var swapped = order.ToList();
        (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
        iso.Db.SetPanelAntigenOrder(id, swapped);

        Assert.Single(Audits(iso, id, "set_antigen_order"));
    }
}
