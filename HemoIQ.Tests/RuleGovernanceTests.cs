using System.Text.Json;
using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class RuleGovernanceTests
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

    private static IsolatedDatabase WithRegistry()
    {
        var iso = new IsolatedDatabase();
        As("adm", () =>
        {
            iso.Db.SaveUser(new AppUser { Login = "adm", Initials = "AD", Name = "Admin", Role = UserRole.Administrator });
            iso.Db.SaveUser(new AppUser { Login = "sup", Initials = "SU", Name = "Sup", Role = UserRole.Supervisor });
            iso.Db.SaveUser(new AppUser { Login = "tech", Initials = "TE", Name = "Tech", Role = UserRole.Technologist });
        });
        return iso;
    }

    [Fact]
    public void Technologist_CannotChangeRules_SupervisorAndAdminCan()
    {
        using var iso = WithRegistry();
        Assert.Throws<UnauthorizedAccessException>(() =>
            As("tech", () => iso.Db.AddRule("Tech rule", null, "anti-K", null, false, 2)));
        Assert.DoesNotContain(iso.Db.GetAllRules(), r => r.Name == "Tech rule");

        var id = As("sup", () => iso.Db.AddRule("Sup rule", null, "anti-K", null, false, 2));
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            As("tech", () => iso.Db.UpdateRule(id, "Sup rule", null, "anti-K", null, true, 1)));
        Assert.Contains("add, edit, or delete analysis rules", ex.Message);
        Assert.Throws<UnauthorizedAccessException>(() => As("tech", () => iso.Db.DeleteRule(id)));
        Assert.Equal(2, iso.Db.GetRule(id)!.MinRuleoutCount);

        As("adm", () => iso.Db.UpdateRule(id, "Sup rule", null, "anti-K", null, false, 1));
        Assert.Equal(1, iso.Db.GetRule(id)!.MinRuleoutCount);
    }

    [Fact]
    public void RuleEditAndDelete_AuditFullBeforeAndAfter_NoOpEditWritesNothing()
    {
        using var iso = new IsolatedDatabase();
        var id = iso.Db.AddRule("Jka homozygous", "dosage", "anti-Jka", null, false, 3);
        iso.Db.UpdateRule(id, "Jka homozygous", "dosage", "anti-Jka", null, true, 2);

        var update = Assert.Single(iso.Db.GetAuditEventsForEntity("rule", id.ToString()), e => e.Action == "update_rule");
        using (var before = JsonDocument.Parse(update.BeforeJson!))
        {
            Assert.Equal(3, before.RootElement.GetProperty("minRuleoutCount").GetInt32());
            Assert.False(before.RootElement.GetProperty("heterozygousOk").GetBoolean());
        }
        using (var after = JsonDocument.Parse(update.AfterJson!))
            Assert.Equal(2, after.RootElement.GetProperty("minRuleoutCount").GetInt32());

        iso.Db.UpdateRule(id, "Jka homozygous", "dosage", "anti-Jka", null, true, 2);
        Assert.Single(iso.Db.GetAuditEventsForEntity("rule", id.ToString()), e => e.Action == "update_rule");

        iso.Db.DeleteRule(id);
        var delete = Assert.Single(iso.Db.GetAuditEventsForEntity("rule", id.ToString()), e => e.Action == "delete_rule");
        Assert.Contains("\"antibody\":\"anti-Jka\"", delete.BeforeJson);
        Assert.Contains("\"heterozygousOk\":true", delete.BeforeJson);

        iso.Db.DeleteRule(id);
        Assert.Single(iso.Db.GetAuditEventsForEntity("rule", id.ToString()), e => e.Action == "delete_rule");
    }

    [Fact]
    public void RuleChangeAfterAnalysis_MarksOpenInvestigationStale_UntilReanalyzed()
    {
        using var iso = new IsolatedDatabase();
        foreach (var acc in new[] { "RG-OPEN", "RG-DONE" })
        {
            iso.Db.AddSpecimen(acc, "serum", null);
            var panelId = iso.Db.AddPanel("RG " + acc, "L-" + acc, "V", 2, null, false, startTypedNegative: true);
            iso.Db.LinkSpecimenPanel(acc, panelId);
            iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
            iso.Db.SaveReaction(acc, panelId, "2", "0", "0", "0", "2+");
            iso.Analyzer.AnalyzeSpecimen(acc, updateDb: true);
        }
        iso.Db.SetSpecimenFinalCall("RG-DONE", "NAD", null, "TE");
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.StaleAnalysis);

        Thread.Sleep(1100); // last_analyzed_at has one-second resolution
        iso.Db.AddRule("New K rule", null, "anti-K", null, false, 2);
        Assert.NotNull(iso.Db.LastAnalysisConfigurationChange());

        var stale = Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.StaleAnalysis);
        Assert.Equal("RG-OPEN", stale.Title);
        Assert.Contains("Analysis rules or Preferences changed", stale.Detail);
        Assert.DoesNotContain("Reactions changed", stale.Detail);

        Thread.Sleep(1100);
        iso.Analyzer.AnalyzeSpecimen("RG-OPEN", updateDb: true);
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.StaleAnalysis);
    }

    [Fact]
    public void PreferencesAuditCountsAsConfigurationChange()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AppendAudit("update_settings", "settings", "lab", null, "{}", "{\"x\":1}");
        Assert.NotNull(iso.Db.LastAnalysisConfigurationChange());
    }
}
