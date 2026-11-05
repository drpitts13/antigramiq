using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class SpecimenHistoryProtectionTests
{
    private static void Reactive(IsolatedDatabase iso, string acc, string? created = null)
    {
        iso.Db.AddSpecimen(acc, "serum", null, createdDate: created);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
    }

    [Fact]
    public void Delete_UnconfirmedSpecimen_IsAudited_WithWhatWasRemoved()
    {
        using var iso = new IsolatedDatabase();
        Reactive(iso, "SH-1");
        iso.Db.UpdateSpecimenMrn("SH-1", "MRN-SH");

        iso.Db.DeleteSpecimen("SH-1");

        Assert.Null(iso.Db.GetSpecimen("SH-1"));
        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "SH-1"), e => e.Action == "delete_specimen");
        Assert.Contains("MRN-SH", audit.BeforeJson);
        Assert.Contains("\"reactionRows\":1", audit.BeforeJson);
    }

    [Fact]
    public void Delete_ConfirmedSpecimen_IsRefused_AndNothingIsRemoved()
    {
        using var iso = new IsolatedDatabase();
        Reactive(iso, "SH-2");
        iso.Db.SetSpecimenFinalCall("SH-2", "anti-K", null, "TE");

        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.DeleteSpecimen("SH-2"));
        Assert.Contains("confirmed identification (anti-K)", ex.Message);
        Assert.Contains("Cancel the investigation instead", ex.Message);
        Assert.NotNull(iso.Db.GetSpecimen("SH-2"));
        Assert.NotEmpty(iso.Db.GetSpecimenPanels("SH-2"));
    }

    [Fact]
    public void Delete_SourceOfEluate_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SH-3", "serum", null);
        iso.Db.AddSpecimen("SH-3E", "eluate", null);
        iso.Db.SetEluateSource("SH-3E", "SH-3");

        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.DeleteSpecimen("SH-3"));
        Assert.Contains("SH-3E", ex.Message);
        Assert.NotNull(iso.Db.GetSpecimen("SH-3"));
    }

    [Fact]
    public void Purge_WithoutArchive_IsRefused_WhenConfirmedIdentificationsWouldBeLost()
    {
        using var iso = new IsolatedDatabase();
        Reactive(iso, "OLD-1", "2023-01-01");
        Reactive(iso, "OLD-2", "2023-02-01");
        iso.Db.SetSpecimenFinalCall("OLD-2", "anti-E", null, "TE");

        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.PurgeSpecimensCreatedBefore("2024-01-01"));
        Assert.Contains("1 of the 2 specimen(s)", ex.Message);
        Assert.Contains("nothing was purged", ex.Message);
        Assert.NotNull(iso.Db.GetSpecimen("OLD-1"));
        Assert.NotNull(iso.Db.GetSpecimen("OLD-2"));
    }

    [Fact]
    public void Purge_WithoutArchive_IsAllowed_WhenNothingIsConfirmed()
    {
        using var iso = new IsolatedDatabase();
        Reactive(iso, "OLD-3", "2023-01-01");
        var result = iso.Db.PurgeSpecimensCreatedBefore("2024-01-01");
        Assert.Equal(1, result.SpecimensDeleted);
        Assert.Null(iso.Db.GetSpecimen("OLD-3"));
    }

    [Fact]
    public void Purge_RequiresAdministrator_OnceUsersAreRegistered()
    {
        using var iso = new IsolatedDatabase();
        Reactive(iso, "OLD-4", "2023-01-01");
        var previous = AccessControl.CurrentLogin;
        try
        {
            AccessControl.CurrentLogin = "adm";
            iso.Db.SaveUser(new AppUser { Login = "adm", Initials = "AD", Role = UserRole.Administrator });
            iso.Db.SaveUser(new AppUser { Login = "sup", Initials = "SU", Role = UserRole.Supervisor });

            AccessControl.CurrentLogin = "sup";
            var ex = Assert.Throws<UnauthorizedAccessException>(() => iso.Db.PurgeSpecimensCreatedBefore("2024-01-01"));
            Assert.Contains("purge specimens", ex.Message);
            Assert.NotNull(iso.Db.GetSpecimen("OLD-4"));

            AccessControl.CurrentLogin = "adm";
            Assert.Equal(1, iso.Db.PurgeSpecimensCreatedBefore("2024-01-01").SpecimensDeleted);
        }
        finally
        {
            AccessControl.CurrentLogin = previous;
        }
    }
}
