using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class ReactionDeletionTests
{
    private static (int defaultRun, int treatedRun) Setup(IsolatedDatabase iso, string acc)
    {
        iso.Db.AddSpecimen(acc, "serum", null);
        var panelId = iso.Db.AddPanel("Del", "L-" + acc, "V", 3, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        var defaultRun = iso.Db.GetOrCreateDefaultRun(acc, panelId);
        iso.Db.SaveReactions(defaultRun, new[]
        {
            new ReactionInput("1", "0", "0", "2+", "NT"),
            new ReactionInput("2", "NT", "NT", "NT", "NT"),
        });
        var treated = iso.Db.AddPanelRun(acc, panelId, CellTreatment.Ficin, SerumTreatment.None, "Ficin");
        iso.Db.SaveReactions(treated, new[] { new ReactionInput("1", "0", "0", "0", "2+") });
        return (defaultRun, treated);
    }

    private static List<AuditEvent> Audits(IsolatedDatabase iso, string acc, string action) =>
        iso.Db.GetAuditEventsForEntity("specimen", acc).Where(e => e.Action == action).ToList();

    [Fact]
    public void Clear_BeforeAnalysis_IsAudited_WithDeletedGrades()
    {
        using var iso = new IsolatedDatabase();
        var (run, _) = Setup(iso, "RD-1");
        Assert.Equal(1, iso.Db.CountGradedReactions(run));

        iso.Db.DeleteReactions(run);

        Assert.Empty(iso.Db.GetReactions(run));
        var audit = Assert.Single(Audits(iso, "RD-1", "clear_reactions"));
        Assert.Null(audit.Reason);
        using var doc = System.Text.Json.JsonDocument.Parse(audit.BeforeJson!);
        Assert.Equal(1, doc.RootElement.GetProperty("gradedCells").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("reactions").GetArrayLength());
    }

    [Fact]
    public void Clear_EmptyRun_WritesNothing()
    {
        using var iso = new IsolatedDatabase();
        var (run, _) = Setup(iso, "RD-2");
        iso.Db.DeleteReactions(run);
        iso.Db.DeleteReactions(run);
        Assert.Single(Audits(iso, "RD-2", "clear_reactions"));
    }

    [Fact]
    public void Clear_AfterAnalysis_NeedsReason_ThenShowsOnTimeline()
    {
        using var iso = new IsolatedDatabase();
        var (run, _) = Setup(iso, "RD-3");
        iso.Db.SetSpecimenLastAnalyzed("RD-3");

        Assert.Throws<ReasonRequiredException>(() => iso.Db.DeleteReactions(run));
        Assert.Equal(2, iso.Db.GetReactions(run).Count);
        Assert.Empty(Audits(iso, "RD-3", "clear_reactions"));

        iso.Db.DeleteReactions(run, "wrong specimen tube");

        Assert.Empty(iso.Db.GetReactions(run));
        Assert.Equal("wrong specimen tube", Assert.Single(Audits(iso, "RD-3", "clear_reactions")).Reason);
        var timeline = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("RD-3")!);
        Assert.Contains(timeline, e => e.What.StartsWith("Reactions cleared on Del") &&
                                       e.What.EndsWith("1 graded cell(s); reason: wrong specimen tube"));
    }

    [Fact]
    public void DeleteRun_AfterAnalysis_NeedsReason_AndIsAudited()
    {
        using var iso = new IsolatedDatabase();
        var (_, treated) = Setup(iso, "RD-4");
        iso.Db.SetSpecimenLastAnalyzed("RD-4");

        Assert.Throws<ReasonRequiredException>(() => iso.Db.DeletePanelRun(treated));
        Assert.NotNull(iso.Db.GetPanelRun(treated));

        iso.Db.DeletePanelRun(treated, "ficin lot expired");

        Assert.Null(iso.Db.GetPanelRun(treated));
        var audit = Assert.Single(Audits(iso, "RD-4", "delete_run"));
        Assert.Equal("ficin lot expired", audit.Reason);
        Assert.Contains("Ficin", audit.BeforeJson);
        Assert.Contains(InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("RD-4")!),
            e => e.What.StartsWith("Run deleted:") && e.What.Contains("reason: ficin lot expired"));
    }

    [Fact]
    public void Deletion_AfterConfirmedIdentification_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        var (run, treated) = Setup(iso, "RD-5");
        iso.Db.SetSpecimenFinalCall("RD-5", "anti-E", null, "TE");

        Assert.Throws<RecordLockedException>(() => iso.Db.DeleteReactions(run, "any"));
        Assert.Throws<RecordLockedException>(() => iso.Db.DeletePanelRun(treated, "any"));
        Assert.Equal(2, iso.Db.GetReactions(run).Count);
    }
}
