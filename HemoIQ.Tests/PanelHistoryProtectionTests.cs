using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Services.Vendors;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class PanelHistoryProtectionTests
{
    private static VendorParseResult Parsed(string lot, params string?[] cellNumbers)
    {
        var parsed = new VendorParseResult
        {
            Vendor = VendorIds.BioRad, LotNumber = lot, Name = "Test " + lot, SourceFormat = "csv",
        };
        foreach (var n in cellNumbers)
        {
            var cell = new PanelCell { CellNumber = n! };
            cell.SetAntigen("D", "+");
            cell.SetAntigen("K", "0");
            parsed.Cells.Add(cell);
        }
        return parsed;
    }

    private static void RecordReaction(IsolatedDatabase iso, string acc, int panelId)
    {
        iso.Db.AddSpecimen(acc, "serum", null);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
    }

    [Fact]
    public void DeletePanel_WithRecordedReactions_IsRefused_AndNothingIsRemoved()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Used", "U-1", "V", 2, null, false);
        RecordReaction(iso, "PH-1", panelId);

        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.DeletePanel(panelId));
        Assert.Contains("holds test history", ex.Message);
        Assert.Contains("Deactivate it instead", ex.Message);
        Assert.NotNull(iso.Db.GetPanel(panelId));
        Assert.Contains(iso.Db.GetSpecimenPanels("PH-1"), p => p.PanelId == panelId);
        Assert.DoesNotContain(iso.Db.GetAuditEventsForEntity("panel", panelId.ToString()), e => e.Action == "delete_panel");
    }

    [Fact]
    public void DeletePanel_LinkedToConfirmedIdentification_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Linked", "U-2", "V", 2, null, false);
        iso.Db.AddSpecimen("PH-2", "serum", null);
        iso.Db.LinkSpecimenPanel("PH-2", panelId);
        iso.Db.SetSpecimenFinalCall("PH-2", "anti-E", null, "TE");

        var usage = iso.Db.GetPanelUsage(panelId);
        Assert.True(usage.ProtectsHistory);
        Assert.Equal(new[] { "PH-2" }, usage.ConfirmedSpecimens);
        Assert.Throws<InvalidOperationException>(() => iso.Db.DeletePanel(panelId));
    }

    [Fact]
    public void DeletePanel_LinkedOnly_IsAllowed_AndAuditNamesTheLinks()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Linked only", "U-3", "V", 2, null, false);
        iso.Db.AddSpecimen("PH-3", "serum", null);
        iso.Db.LinkSpecimenPanel("PH-3", panelId);
        Assert.False(iso.Db.GetPanelUsage(panelId).ProtectsHistory);

        iso.Db.DeletePanel(panelId);

        Assert.Null(iso.Db.GetPanel(panelId));
        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("panel", panelId.ToString()), e => e.Action == "delete_panel");
        Assert.Contains("PH-3", audit.BeforeJson);
    }

    [Fact]
    public void VendorReplace_FailingPartWay_KeepsTheStoredLot()
    {
        using var iso = new IsolatedDatabase();
        var importer = new VendorPanelImportService(iso.Db);
        var originalId = importer.Persist(Parsed("R-1", "1", "2"));

        // The third cell has no cell number, which the database refuses after the
        // old lot was deleted and the new panel row was added.
        Assert.ThrowsAny<Exception>(() => importer.Persist(Parsed("R-1", "1", "2", null), replaceExisting: true));

        Assert.NotNull(iso.Db.GetPanel(originalId));
        Assert.Equal(2, iso.Db.GetPanelCells(originalId).Count);
        Assert.Equal(originalId, iso.Db.FindPanelByVendorLot(VendorIds.BioRad, "R-1")!.PanelId);
        Assert.DoesNotContain(iso.Db.GetAuditEventsForEntity("panel", originalId.ToString()), e => e.Action == "delete_panel");
    }

    [Fact]
    public void VendorReplace_WhenStoredLotHasTestHistory_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        var importer = new VendorPanelImportService(iso.Db);
        var originalId = importer.Persist(Parsed("R-2", "1", "2"));
        RecordReaction(iso, "PH-4", originalId);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            importer.Persist(Parsed("R-2", "1", "2", "3"), replaceExisting: true));
        Assert.Contains("holds test history", ex.Message);
        Assert.Contains("The stored panel is unchanged", ex.Message);
        Assert.Equal(2, iso.Db.GetPanelCells(originalId).Count);
        Assert.Contains(iso.Db.GetSpecimenPanels("PH-4"), p => p.PanelId == originalId);
    }

    [Fact]
    public void VendorReplace_WithoutHistory_ReplacesTheLot()
    {
        using var iso = new IsolatedDatabase();
        var importer = new VendorPanelImportService(iso.Db);
        var originalId = importer.Persist(Parsed("R-3", "1", "2"));

        var newId = importer.Persist(Parsed("R-3", "1", "2", "3"), replaceExisting: true);

        Assert.NotEqual(originalId, newId);
        Assert.Null(iso.Db.GetPanel(originalId));
        Assert.Equal(3, iso.Db.GetPanelCells(newId).Count);
    }

    [Fact]
    public void ReplacePanelCells_FailingPartWay_KeepsTheOldCells()
    {
        using var iso = new IsolatedDatabase();
        var panelId = iso.Db.AddPanel("Cells", "C-1", "V", 3, null, false);
        Assert.Equal(3, iso.Db.GetPanelCells(panelId).Count);

        var bad = new List<PanelCell> { new() { CellNumber = "1" }, new() { CellNumber = null! } };
        Assert.ThrowsAny<Exception>(() => iso.Db.ReplacePanelCells(panelId, bad));

        Assert.Equal(new[] { "1", "2", "3" }, iso.Db.GetPanelCells(panelId).Select(c => c.CellNumber));
    }
}
