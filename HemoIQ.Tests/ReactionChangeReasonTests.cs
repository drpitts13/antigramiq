using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class ReactionChangeReasonTests
{
    private static (int runId, int panelId) Setup(IsolatedDatabase iso, string acc)
    {
        iso.Db.AddSpecimen(acc, "serum", null);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 3, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReactions(RunOf(iso, acc, panelId), new[]
        {
            new ReactionInput("1", "0", "0", "2+", "NT"),
            new ReactionInput("2", "0", "0", "0", "2+"),
            new ReactionInput("3", "NT", "NT", "NT", "NT"),
        });
        return (RunOf(iso, acc, panelId), panelId);
    }

    private static int RunOf(IsolatedDatabase iso, string acc, int panelId) =>
        iso.Db.GetOrCreateDefaultRun(acc, panelId);

    private static readonly ReactionInput[] Edited =
    {
        new("1", "0", "0", "0", "2+"),     // AHG 2+ → 0, CC NT → 2+ (NT was not a result)
        new("2", "0", "0", "0", "2+"),     // unchanged
        new("3", "0", "0", "1+", "NT"),    // first entry
    };

    [Fact]
    public void DescribeChanges_ListsOnlyReplacedResults()
    {
        using var iso = new IsolatedDatabase();
        var (runId, _) = Setup(iso, "RC-1");

        var changes = iso.Db.DescribeReactionChanges(runId, Edited);

        Assert.Equal(new[] { "Cell 1 AHG: 2+ → 0" }, changes);
    }

    [Fact]
    public void BeforeAnalysis_ChangesNeedNoReason()
    {
        using var iso = new IsolatedDatabase();
        var (runId, _) = Setup(iso, "RC-2");

        iso.Db.SaveReactions(runId, Edited);

        Assert.Equal("0", iso.Db.GetReactions(runId).First(r => r.CellNumber == "1").AHG);
    }

    [Fact]
    public void AfterAnalysis_ChangeWithoutReason_IsRefused_AndNothingIsSaved()
    {
        using var iso = new IsolatedDatabase();
        var (runId, _) = Setup(iso, "RC-3");
        iso.Db.SetSpecimenLastAnalyzed("RC-3");
        var auditsBefore = iso.Db.GetAuditEventsForEntity("specimen", "RC-3").Count;

        var ex = Assert.Throws<ReasonRequiredException>(() => iso.Db.SaveReactions(runId, Edited));
        Assert.Contains("Cell 1 AHG: 2+ → 0", ex.Message);
        Assert.Single(ex.Changes);

        var rows = iso.Db.GetReactions(runId);
        Assert.Equal("2+", rows.First(r => r.CellNumber == "1").AHG);
        Assert.Equal("NT", rows.First(r => r.CellNumber == "3").AHG); // first entry on cell 3 was not saved either
        Assert.Equal(auditsBefore, iso.Db.GetAuditEventsForEntity("specimen", "RC-3").Count);
    }

    [Fact]
    public void AfterAnalysis_ChangeWithReason_IsSaved_AndReasonIsOnAuditAndTimeline()
    {
        using var iso = new IsolatedDatabase();
        var (runId, _) = Setup(iso, "RC-4");
        iso.Db.SetSpecimenLastAnalyzed("RC-4");

        iso.Db.SaveReactions(runId, Edited, "transcription error, re-read tube");

        Assert.Equal("0", iso.Db.GetReactions(runId).First(r => r.CellNumber == "1").AHG);
        var saves = iso.Db.GetAuditEventsForEntity("specimen", "RC-4").Where(e => e.Action == "save_reaction").ToList();
        var change = Assert.Single(saves, UserActivityService.IsReactionChange);
        Assert.Equal("transcription error, re-read tube", change.Reason);
        Assert.Null(saves.Last(e => e.AfterJson!.Contains("\"cellNumber\":\"3\"")).Reason); // first entry carries no reason

        var timeline = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("RC-4")!);
        Assert.Contains(timeline, e => e.What.Contains("1 recorded result(s) changed") &&
                                       e.What.Contains("reason: transcription error, re-read tube"));
    }

    [Fact]
    public void AfterAnalysis_FirstEntriesOnly_NeedNoReason()
    {
        using var iso = new IsolatedDatabase();
        var (runId, _) = Setup(iso, "RC-5");
        iso.Db.SetSpecimenLastAnalyzed("RC-5");

        iso.Db.SaveReactions(runId, new[] { new ReactionInput("3", "0", "0", "1+", "NT") });

        Assert.Equal("1+", iso.Db.GetReactions(runId).First(r => r.CellNumber == "3").AHG);
    }
}
