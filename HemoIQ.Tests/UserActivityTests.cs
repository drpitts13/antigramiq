using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class UserActivityTests
{
    private static T As<T>(string login, Func<T> body)
    {
        var previous = AccessControl.CurrentLogin;
        try { AccessControl.CurrentLogin = login; return body(); }
        finally { AccessControl.CurrentLogin = previous; }
    }

    private static void As(string login, Action body) => As(login, () => { body(); return 0; });

    private static int Setup(IsolatedDatabase iso, string acc)
    {
        iso.Db.AddSpecimen(acc, "serum", null);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 3, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        return panelId;
    }

    private static List<AuditEvent> ReactionAudits(IsolatedDatabase iso, string acc) =>
        iso.Db.GetAuditEventsForEntity("specimen", acc).Where(e => e.Action == "save_reaction").ToList();

    [Fact]
    public void SaveReaction_Unchanged_WritesNoAudit()
    {
        using var iso = new IsolatedDatabase();
        var panelId = Setup(iso, "UA-1");
        iso.Db.SaveReaction("UA-1", panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction("UA-1", panelId, "1", "0", "0", "0", "2+");

        Assert.Single(ReactionAudits(iso, "UA-1"));
    }

    [Fact]
    public void SaveReaction_Change_RecordsOriginalValue()
    {
        using var iso = new IsolatedDatabase();
        var panelId = Setup(iso, "UA-2");
        iso.Db.SaveReaction("UA-2", panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction("UA-2", panelId, "1", "0", "0", "0", "0");

        var audits = ReactionAudits(iso, "UA-2");
        Assert.Equal(2, audits.Count);
        Assert.Null(audits[0].BeforeJson);
        Assert.False(UserActivityService.IsReactionChange(audits[0]));
        static string Cc(string? json) =>
            System.Text.Json.JsonDocument.Parse(json!).RootElement.GetProperty("cc").GetString()!;
        Assert.Equal("2+", Cc(audits[1].BeforeJson));
        Assert.Equal("0", Cc(audits[1].AfterJson));
        Assert.True(UserActivityService.IsReactionChange(audits[1]));
    }

    [Fact]
    public void Timeline_NamesChangedResults()
    {
        using var iso = new IsolatedDatabase();
        var panelId = Setup(iso, "UA-3");
        iso.Db.SaveReaction("UA-3", panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction("UA-3", panelId, "2", "0", "0", "0", "0");
        iso.Db.SaveReaction("UA-3", panelId, "1", "0", "0", "0", "1+");

        var timeline = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("UA-3")!);
        Assert.Contains(timeline, e => e.What.Contains("Reactions entered: 3 save(s), 2 cell(s)") &&
                                       e.What.Contains("1 recorded result(s) changed"));
    }

    [Fact]
    public void Activity_CountsPerLogin_WithRegisteredNames()
    {
        using var iso = new IsolatedDatabase();
        var p1 = Setup(iso, "UA-4");
        var p2 = Setup(iso, "UA-5");
        As("adm", () =>
        {
            iso.Db.SaveUser(new AppUser { Login = "adm", Initials = "AD", Name = "Admin", Role = UserRole.Administrator });
            iso.Db.SaveUser(new AppUser { Login = "tech", Initials = "TE", Name = "Tess", Role = UserRole.Technologist });
            iso.Db.SaveUser(new AppUser { Login = "sup", Initials = "SU", Name = "Sam", Role = UserRole.Supervisor });
        });
        As("tech", () =>
        {
            iso.Db.SaveReaction("UA-4", p1, "1", "0", "0", "0", "2+");
            iso.Db.SaveReaction("UA-4", p1, "2", "0", "0", "0", "0");
            iso.Db.SaveReaction("UA-5", p2, "1", "0", "0", "0", "0");
            iso.Db.SaveReaction("UA-4", p1, "1", "0", "0", "0", "3+");   // change
            iso.Db.SetSpecimenFinalCall("UA-4", "anti-E", null, "TE");
        });
        As("sup", () =>
        {
            iso.Db.SetSpecimenSupervisorReview("UA-4", "SU");
            iso.Db.ClearSpecimenFinalCall("UA-4", "wrong antibody");
        });

        var rows = UserActivityService.Compute(iso.Db, DateTime.Today.AddDays(-1), DateTime.Today);

        var tech = Assert.Single(rows, r => r.Login == "tech");
        Assert.Equal("Tess (TE) [tech]", tech.Display);
        Assert.Equal(3, tech.ReactionEntries);
        Assert.Equal(1, tech.ReactionChanges);
        Assert.Equal(2, tech.Specimens);
        Assert.Equal(1, tech.Confirmations);

        var sup = Assert.Single(rows, r => r.Login == "sup");
        Assert.Equal(1, sup.Reviews);
        Assert.Equal(1, sup.Clears);
        Assert.Equal(0, sup.ReactionEntries);
    }

    [Fact]
    public void Activity_RespectsDateRange_AndGroupsLoginsCaseInsensitively()
    {
        var events = new[]
        {
            new AuditEvent { Action = "save_reaction", Operator = "Tech", EntityType = "specimen", EntityId = "A",
                             OccurredAtUtc = DateTime.UtcNow.ToString("o") },
            new AuditEvent { Action = "save_reaction", Operator = "tech", EntityType = "specimen", EntityId = "B",
                             OccurredAtUtc = DateTime.UtcNow.ToString("o") },
            new AuditEvent { Action = "confirm_final_call", Operator = "tech", EntityType = "specimen", EntityId = "C",
                             OccurredAtUtc = DateTime.UtcNow.AddDays(-30).ToString("o") },
        };

        var row = Assert.Single(UserActivityService.Compute(events, DateTime.Today.AddDays(-1), DateTime.Today));
        Assert.Equal(2, row.ReactionEntries);
        Assert.Equal(0, row.Confirmations);
        Assert.Equal("Tech", row.Display);
        Assert.Equal(3, Assert.Single(UserActivityService.Compute(events, null, null)).Total);
    }

    [Fact]
    public void ActivityCsv_HasHeaderAndRow()
    {
        var lines = AnalyticsViewModel.UserActivityCsvLines(new[]
        {
            new UserActivity { Login = "tech", Name = "Doe, T", ReactionEntries = 5, ReactionChanges = 1, Specimens = 2, Confirmations = 1 },
        }).ToList();
        Assert.Contains("Login,Name,Reaction cells entered,Recorded results changed,Specimens,Identifications confirmed,Identifications cleared,Supervisor reviews", lines);
        Assert.Contains("tech,\"Doe, T\",5,1,2,1,0,0", lines);
    }
}
