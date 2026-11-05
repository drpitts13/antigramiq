using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class ExtraAntigenReviewTests
{
    /// <summary>Adds an extra-antigen column the way versions before the NT default did: every cell "-", no audit entry.</summary>
    private static void AddLegacyColumn(IsolatedDatabase iso, int panelId, string antigen)
    {
        using var conn = new SqliteConnection($"Data Source={iso.DbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO panel_extra_antigens (panel_id, antigen_name) VALUES ($pid, $ag);
            INSERT INTO panel_cell_extra_antigens (cell_id, antigen_name, value)
            SELECT id, $ag, '-' FROM panel_cells WHERE panel_id = $pid;";
        cmd.Parameters.AddWithValue("$pid", panelId);
        cmd.Parameters.AddWithValue("$ag", antigen);
        cmd.ExecuteNonQuery();
    }

    private static List<WorklistItem> ReviewItems(IsolatedDatabase iso) =>
        iso.Db.GetWorklistItems().Where(i => i.Kind == WorklistKind.ExtraAntigenReview).ToList();

    [Fact]
    public void LegacyAllNegativeColumn_IsListedForReview()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Legacy", "LG-1", "V", 3, null, false);
        AddLegacyColumn(iso, id, "Doa");

        var item = Assert.Single(ReviewItems(iso));
        Assert.Equal(id, item.PanelId);
        Assert.Equal("Doa", item.Antigen);
        Assert.Equal("Panels", item.TargetTab);
        Assert.Contains("Doa is negative on all 3 cells", item.Detail);
    }

    [Fact]
    public void ColumnAddedNow_OrWithAPositiveCell_IsNotListed()
    {
        using var iso = new IsolatedDatabase();
        var a = iso.Db.AddPanel("New", "LG-2", "V", 3, null, false);
        iso.Db.AddPanelExtraAntigen(a, "Doa");                       // starts NT and is audited
        foreach (var c in iso.Db.GetPanelCells(a)) iso.Db.UpdatePanelCellAntigen(c.Id, "Doa", "-");

        var b = iso.Db.AddPanel("Mixed", "LG-3", "V", 3, null, false);
        AddLegacyColumn(iso, b, "Dob");
        iso.Db.UpdatePanelCellAntigen(iso.Db.GetPanelCells(b)[0].Id, "Dob", "+");

        Assert.Empty(ReviewItems(iso));
    }

    [Fact]
    public void AuditedTypingEdit_ClearsTheItem()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Legacy", "LG-4", "V", 3, null, false);
        AddLegacyColumn(iso, id, "Doa");
        var cells = iso.Db.GetPanelCells(id);
        cells[0].Antigens["Doa"] = "NT";
        iso.Db.SavePanelCellEdits(id, cells);
        cells = iso.Db.GetPanelCells(id);
        cells[0].Antigens["Doa"] = "-";
        iso.Db.SavePanelCellEdits(id, cells);

        Assert.Empty(ReviewItems(iso));
    }

    [Fact]
    public void Verify_ClearsTheItem_IsAudited_AndChangesNoTyping()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Legacy", "LG-5", "V", 3, null, false);
        AddLegacyColumn(iso, id, "Doa");

        iso.Db.VerifyExtraAntigenTyping(id, "Doa");

        Assert.Empty(ReviewItems(iso));
        Assert.Single(iso.Db.GetAuditEventsForEntity("panel", id.ToString()), e => e.Action == "verify_extra_antigen");
        Assert.All(iso.Db.GetPanelCells(id), c => Assert.Equal("-", c.Antigens["Doa"]));
        Assert.Throws<ArgumentException>(() => iso.Db.VerifyExtraAntigenTyping(id, "Dob"));
    }

    [Fact]
    public void Verify_RequiresSupervisor_OnceUsersAreRegistered()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddPanel("Legacy", "LG-6", "V", 3, null, false);
        AddLegacyColumn(iso, id, "Doa");
        var previous = AccessControl.CurrentLogin;
        try
        {
            AccessControl.CurrentLogin = "adm";
            iso.Db.SaveUser(new AppUser { Login = "adm", Initials = "AD", Role = UserRole.Administrator });
            iso.Db.SaveUser(new AppUser { Login = "tech", Initials = "TE", Role = UserRole.Technologist });
            AccessControl.CurrentLogin = "tech";
            Assert.Throws<UnauthorizedAccessException>(() => iso.Db.VerifyExtraAntigenTyping(id, "Doa"));
            Assert.Single(ReviewItems(iso));
        }
        finally
        {
            AccessControl.CurrentLogin = previous;
        }
    }
}
