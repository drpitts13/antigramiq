using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class ExpiredReagentTests
{
    private static readonly string Yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
    private static readonly string NextMonth = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");

    private static int Tested(IsolatedDatabase iso, string acc, string? expiration, bool grade = true)
    {
        iso.Db.AddSpecimen(acc, "serum", null);
        var panelId = iso.Db.AddPanel("Lot " + acc, "L-" + acc, "V", 3, expiration, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        var run = iso.Db.GetOrCreateDefaultRun(acc, panelId);
        iso.Db.SaveReactions(run, new[]
        {
            new ReactionInput("1", grade ? "0" : "NT", grade ? "0" : "NT", grade ? "2+" : "NT", "NT"),
            new ReactionInput("2", grade ? "0" : "NT", grade ? "0" : "NT", grade ? "0" : "NT", grade ? "2+" : "NT"),
        });
        return panelId;
    }

    [Fact]
    public void ReactionsRecordedAfterExpiry_AreFound()
    {
        using var iso = new IsolatedDatabase();
        Tested(iso, "EXR-1", Yesterday);

        var finding = Assert.Single(ExpiredReagentCheck.Check(iso.Db, "EXR-1"));
        Assert.Equal(Yesterday, finding.Expiration);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), finding.RecordedOn);
        Assert.Contains("after the lot expired", finding.Note);
        Assert.Contains("Not an exclusion", finding.Note);
    }

    [Fact]
    public void InDateLot_NoExpiry_OrNoGrades_AreNotFound()
    {
        using var iso = new IsolatedDatabase();
        Tested(iso, "EXR-2", NextMonth);
        Tested(iso, "EXR-3", null);
        Tested(iso, "EXR-4", Yesterday, grade: false);

        Assert.Empty(ExpiredReagentCheck.Check(iso.Db, "EXR-2"));
        Assert.Empty(ExpiredReagentCheck.Check(iso.Db, "EXR-3"));
        Assert.Empty(ExpiredReagentCheck.Check(iso.Db, "EXR-4"));
    }

    [Fact]
    public void Analysis_ShowsReviewNote_ForExpiredLot()
    {
        using var iso = new IsolatedDatabase();
        Tested(iso, "EXR-5", Yesterday);

        var result = new AntibodyAnalyzer(iso.Db).AnalyzeSpecimen("EXR-5");

        Assert.Contains(result.TransfusionRequirementNotes, n => n.Contains("after the lot expired " + Yesterday));
    }

    [Fact]
    public void Worklist_ListsOpenInvestigation_UntilReviewed()
    {
        using var iso = new IsolatedDatabase();
        Tested(iso, "EXR-6", Yesterday);
        Tested(iso, "EXR-7", NextMonth);

        var items = iso.Db.GetWorklistItems().Where(i => i.Kind == WorklistKind.ExpiredReagentUse).ToList();
        var item = Assert.Single(items);
        Assert.Equal("EXR-6", item.AccessionNumber);
        Assert.Equal("Analysis", item.TargetTab);

        iso.Db.SetSpecimenFinalCall("EXR-6", "anti-E", null, "TE");
        Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.ExpiredReagentUse);
        iso.Db.SetSpecimenSupervisorReview("EXR-6", "SU");
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.ExpiredReagentUse);
    }
}
