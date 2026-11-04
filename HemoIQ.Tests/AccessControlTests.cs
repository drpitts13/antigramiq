using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class AccessControlTests
{
    private static T As<T>(string login, Func<T> body)
    {
        var previous = AccessControl.CurrentLogin;
        try
        {
            AccessControl.CurrentLogin = login;
            return body();
        }
        finally
        {
            AccessControl.CurrentLogin = previous;
        }
    }

    private static void As(string login, Action body) => As(login, () => { body(); return 0; });

    /// <summary>Registry with admin "adm" (AD), supervisor "sup" (SU), technologist "tech" (TE).</summary>
    private static IsolatedDatabase WithRegistry()
    {
        var iso = new IsolatedDatabase();
        As("adm", () =>
        {
            iso.Db.SaveUser(new AppUser { Login = "adm", Initials = "AD", Name = "Admin", Role = UserRole.Administrator });
            iso.Db.SaveUser(new AppUser { Login = "sup", Initials = "SU", Name = "Sup", Role = UserRole.Supervisor });
            iso.Db.SaveUser(new AppUser { Login = "tech", Initials = "TE", Name = "Tech", Role = UserRole.Technologist });
        });
        iso.Db.AddSpecimen("AC-1", "serum", null);
        As("tech", () => iso.Db.SetSpecimenFinalCall("AC-1", "anti-E", null, "TE"));
        return iso;
    }

    [Fact]
    public void EmptyRegistry_AllowsEverything()
    {
        using var iso = new IsolatedDatabase();
        Assert.False(AccessControl.RegistryEnabled(iso.Db));
        foreach (var p in Enum.GetValues<Permission>())
            Assert.True(As("anyone", () => AccessControl.Can(iso.Db, p)));
    }

    [Fact]
    public void Technologist_IsRefusedReviewClearAndPreferences()
    {
        using var iso = WithRegistry();
        As("tech", () =>
        {
            Assert.False(AccessControl.Can(iso.Db, Permission.ConfigurePreferences));
            var ex = Assert.Throws<UnauthorizedAccessException>(() => iso.Db.SetSpecimenSupervisorReview("AC-1", "TE"));
            Assert.Contains("Tech (TE) (Technologist) cannot record a supervisor review", ex.Message);
            Assert.Throws<UnauthorizedAccessException>(() => iso.Db.ClearSpecimenFinalCall("AC-1", "wrong"));
        });
        Assert.True(iso.Db.GetSpecimen("AC-1")!.HasFinalCall);
    }

    [Fact]
    public void Supervisor_CanReviewWithOwnInitialsOnly_ButNotChangePreferences()
    {
        using var iso = WithRegistry();
        As("sup", () =>
        {
            Assert.False(AccessControl.Can(iso.Db, Permission.ConfigurePreferences));
            var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.SetSpecimenSupervisorReview("AC-1", "AD"));
            Assert.Contains("Enter your own initials (SU)", ex.Message);
            iso.Db.SetSpecimenSupervisorReview("AC-1", "su");
        });
        Assert.Equal("su", iso.Db.GetSpecimen("AC-1")!.ReviewedBy);
        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "AC-1"), e => e.Action == "supervisor_review");
        Assert.Equal("sup", audit.Operator);
    }

    [Fact]
    public void UnregisteredLogin_IsRefused_WithClearMessage()
    {
        using var iso = WithRegistry();
        As("stranger", () =>
        {
            var ex = Assert.Throws<UnauthorizedAccessException>(() =>
                AccessControl.Demand(iso.Db, Permission.SupervisorReview));
            Assert.Contains("'stranger' is not an active HemoIQ user", ex.Message);
        });
    }

    [Fact]
    public void Administrator_CanDoEverything()
    {
        using var iso = WithRegistry();
        As("adm", () =>
        {
            foreach (var p in Enum.GetValues<Permission>())
                Assert.True(AccessControl.Can(iso.Db, p));
        });
    }

    [Fact]
    public void FirstUserMustBeAdmin_AndLastAdminCannotBeDemotedOrDeactivated()
    {
        using var iso = new IsolatedDatabase();
        As("x", () =>
        {
            var first = Assert.Throws<InvalidOperationException>(() =>
                iso.Db.SaveUser(new AppUser { Login = "x", Initials = "XX", Role = UserRole.Technologist }));
            Assert.Contains("first user must be an active Administrator", first.Message);

            iso.Db.SaveUser(new AppUser { Login = "x", Initials = "XX", Role = UserRole.Administrator });
            Assert.Throws<InvalidOperationException>(() =>
                iso.Db.SaveUser(new AppUser { Login = "x", Initials = "XX", Role = UserRole.Supervisor }));
            Assert.Throws<InvalidOperationException>(() =>
                iso.Db.SaveUser(new AppUser { Login = "x", Initials = "XX", Role = UserRole.Administrator, Active = false }));
        });
        Assert.Single(iso.Db.GetUsers());
    }

    [Fact]
    public void ManagingUsers_RequiresAdmin_RejectsDuplicateInitials_AndIsAudited()
    {
        using var iso = WithRegistry();
        As("sup", () => Assert.Throws<UnauthorizedAccessException>(() =>
            iso.Db.SaveUser(new AppUser { Login = "new", Initials = "NW", Role = UserRole.Technologist })));
        As("adm", () =>
        {
            Assert.Throws<ArgumentException>(() =>
                iso.Db.SaveUser(new AppUser { Login = "dup", Initials = "te", Role = UserRole.Technologist }));
            iso.Db.SaveUser(new AppUser { Login = "tech", Initials = "TE", Name = "Tech", Role = UserRole.Supervisor });
        });
        var ev = Assert.Single(iso.Db.GetAuditEventsForEntity("user", "tech"), e => e.Action == "update_user");
        Assert.Contains("technologist", ev.BeforeJson);
        Assert.Contains("supervisor", ev.AfterJson);
        Assert.Equal("adm", ev.Operator);
    }

    [Fact]
    public void LoginMatching_IsCaseInsensitive()
    {
        using var iso = WithRegistry();
        Assert.True(As("SUP", () => AccessControl.Can(iso.Db, Permission.SupervisorReview)));
    }
}
