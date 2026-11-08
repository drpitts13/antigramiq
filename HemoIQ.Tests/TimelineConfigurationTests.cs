using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

public class TimelineConfigurationTests
{
    private static List<string> Timeline(IsolatedDatabase iso, string acc) =>
        InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen(acc)!).Select(e => e.What).ToList();

    [Fact]
    public void RuleChanges_WhileOpen_AppearWithWhatChanged()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TC-1", "serum", null);
        var id = iso.Db.AddRule("Jka dosage", null, "anti-Jka", null, false, 3);
        iso.Db.UpdateRule(id, "Jka dosage", null, "anti-Jka", null, true, 2);
        iso.Db.DeleteRule(id);

        var t = Timeline(iso, "TC-1");
        Assert.Contains(t, w => w.StartsWith("Analysis rule added: Jka dosage (anti-Jka)") && w.Contains("lab configuration"));
        Assert.Contains(t, w => w.StartsWith("Analysis rule changed: Jka dosage;") &&
                                w.Contains("heterozygous cells allowed no → yes") &&
                                w.Contains("minimum rule-out cells 3 → 2"));
        Assert.Contains(t, w => w.StartsWith("Analysis rule deleted: Jka dosage (anti-Jka)"));
    }

    [Fact]
    public void RuleChange_AfterConfirmation_IsNotShown_UntilIdentificationIsCleared()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TC-2", "serum", null);
        iso.Db.SetSpecimenFinalCall("TC-2", "anti-E", null, "TE");
        iso.Db.AddRule("After confirm", null, "anti-K", null, false, 2);
        Assert.DoesNotContain(Timeline(iso, "TC-2"), w => w.Contains("Analysis rule"));

        iso.Db.ClearSpecimenFinalCall("TC-2", "wrong panel lot");
        iso.Db.AddRule("While reopened", null, "anti-S", null, false, 2);
        var t = Timeline(iso, "TC-2");
        Assert.DoesNotContain(t, w => w.Contains("After confirm"));
        Assert.Contains(t, w => w.Contains("Analysis rule added: While reopened (anti-S)"));
    }

    [Fact]
    public void PreferencesChange_ShowsChangedSettingsOnly()
    {
        var a = new AuditEvent
        {
            Action = "update_settings",
            OccurredAtUtc = DateTime.UtcNow.ToString("o"),
            BeforeJson = "{\"SeparationCellCount\":1,\"IdentificationCellCount\":3}",
            AfterJson = "{\"SeparationCellCount\":2,\"IdentificationCellCount\":3}",
        };
        var text = InvestigationTimeline.DescribeConfiguration(a);
        Assert.StartsWith("Preferences changed: SeparationCellCount 1 → 2", text);
        Assert.DoesNotContain("IdentificationCellCount", text);
        Assert.Null(InvestigationTimeline.DescribeConfiguration(new AuditEvent { Action = "add_panel" }));
    }

    [Fact]
    public void OpenPeriods_FollowConfirmClearCancelReinstate()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        AuditEvent E(long id, string action, int hours) =>
            new() { Id = id, Action = action, OccurredAtUtc = t0.AddHours(hours).ToString("o") };
        var periods = InvestigationTimeline.OpenPeriods(new DateTime(2026, 11, 1), new[]
        {
            E(1, "confirm_final_call", 1), E(2, "clear_final_call", 2),
            E(3, "cancel_investigation", 3), E(4, "reinstate_investigation", 4),
        });
        Assert.Equal(3, periods.Count);
        Assert.Equal(t0.AddHours(1).ToLocalTime(), periods[0].To);
        Assert.Equal((t0.AddHours(2).ToLocalTime(), t0.AddHours(3).ToLocalTime()), periods[1]);
        Assert.Equal(DateTime.MaxValue, periods[2].To);
    }
}
