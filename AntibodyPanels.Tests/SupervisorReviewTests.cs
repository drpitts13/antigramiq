using AntibodyPanels.Models;
using AntibodyPanels.Services;
using AntibodyPanels.Tests.Infrastructure;

namespace AntibodyPanels.Tests;

[Collection("PersistentDatabase")]
public class SupervisorReviewTests
{
    private static T WithReviewRequired<T>(bool required, Func<T> body)
    {
        var previous = AppSettings.Current.RequireSupervisorReview;
        try
        {
            AppSettings.Current.RequireSupervisorReview = required;
            return body();
        }
        finally
        {
            AppSettings.Current.RequireSupervisorReview = previous;
        }
    }

    private static IsolatedDatabase Confirmed(string acc, string by = "DP")
    {
        var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen(acc, "serum", null);
        iso.Db.SetSpecimenFinalCall(acc, "anti-E", null, by);
        return iso;
    }

    [Fact]
    public void Review_IsRecordedAndAudited_ByADifferentPerson()
    {
        using var iso = Confirmed("SR-1");
        iso.Db.SetSpecimenSupervisorReview("SR-1", " ab ", "pattern and rule-outs checked");
        var s = iso.Db.GetSpecimen("SR-1")!;
        Assert.Equal("ab", s.ReviewedBy);
        Assert.False(string.IsNullOrEmpty(s.ReviewedAt));
        var ev = Assert.Single(iso.Db.GetAuditEvents(), e => e.Action == "supervisor_review");
        Assert.Equal("pattern and rule-outs checked", ev.Reason);
        Assert.Contains("anti-E", ev.AfterJson);
    }

    [Fact]
    public void Review_BySamePersonWhoConfirmed_IsRefused_CaseInsensitive()
    {
        using var iso = Confirmed("SR-2", "DP");
        var ex = Assert.Throws<InvalidOperationException>(() => iso.Db.SetSpecimenSupervisorReview("SR-2", "dp"));
        Assert.Contains("someone other than", ex.Message);
        Assert.False(iso.Db.GetSpecimen("SR-2")!.IsReviewed);
    }

    [Fact]
    public void Review_WithoutFinalCall_Twice_OrBlankInitials_IsRefused()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SR-3", "serum", null);
        Assert.Throws<InvalidOperationException>(() => iso.Db.SetSpecimenSupervisorReview("SR-3", "AB"));

        iso.Db.SetSpecimenFinalCall("SR-3", "anti-K", null, "DP");
        Assert.Throws<ArgumentException>(() => iso.Db.SetSpecimenSupervisorReview("SR-3", "  "));
        iso.Db.SetSpecimenSupervisorReview("SR-3", "AB");
        Assert.Throws<InvalidOperationException>(() => iso.Db.SetSpecimenSupervisorReview("SR-3", "CD"));
    }

    [Fact]
    public void ClearingOrReconfirming_InvalidatesTheReview()
    {
        using var iso = Confirmed("SR-4");
        iso.Db.SetSpecimenSupervisorReview("SR-4", "AB");
        iso.Db.ClearSpecimenFinalCall("SR-4", "wrong call");
        Assert.False(iso.Db.GetSpecimen("SR-4")!.IsReviewed);

        iso.Db.SetSpecimenFinalCall("SR-4", "anti-c", null, "DP");
        Assert.False(iso.Db.GetSpecimen("SR-4")!.IsReviewed);
    }

    [Fact]
    public void Status_WhenRequired_WaitsForReview_ThenCompletes()
    {
        using var iso = Confirmed("SR-5");
        var pending = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("SR-5")!, requireReview: true);
        Assert.Equal(InvestigationStatusService.AwaitingSupervisorReview, pending.Label);
        Assert.Contains("other than DP", pending.NextAction);
        Assert.Equal("Supervisor reviewed", pending.Steps.Last().Name);
        Assert.False(pending.Steps.Last().Done);

        iso.Db.SetSpecimenSupervisorReview("SR-5", "AB");
        var done = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("SR-5")!, requireReview: true);
        Assert.Equal(InvestigationStatusService.Completed, done.Label);
        Assert.All(done.Steps.Skip(4), s => Assert.True(s.Done));
    }

    [Fact]
    public void Status_WhenNotRequired_HasNoReviewStep_AndCompletesOnConfirm()
    {
        using var iso = Confirmed("SR-6");
        var st = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("SR-6")!, requireReview: false);
        Assert.Equal(InvestigationStatusService.Completed, st.Label);
        Assert.DoesNotContain(st.Steps, s => s.Name == "Supervisor reviewed");
    }

    [Fact]
    public void Worklist_ListsUnreviewedOnlyWhenRequired()
    {
        using var iso = Confirmed("SR-7");
        var required = WithReviewRequired(true, () => iso.Db.GetWorklistItems());
        var item = Assert.Single(required, i => i.Kind == WorklistKind.AwaitingSupervisorReview);
        Assert.Equal("SR-7", item.AccessionNumber);
        Assert.Contains("anti-E", item.Detail);

        Assert.DoesNotContain(WithReviewRequired(false, () => iso.Db.GetWorklistItems()),
            i => i.Kind == WorklistKind.AwaitingSupervisorReview);

        iso.Db.SetSpecimenSupervisorReview("SR-7", "AB");
        Assert.DoesNotContain(WithReviewRequired(true, () => iso.Db.GetWorklistItems()),
            i => i.Kind == WorklistKind.AwaitingSupervisorReview);
    }

    [Fact]
    public void Worksheet_ShowsPendingThenReviewer()
    {
        using var iso = Confirmed("SR-8");
        var reports = new ReportService(iso.Db);
        var pending = WithReviewRequired(true,
            () => reports.GeneratePreviewText(ReportType.ClinicalIdentification, "SR-8"));
        Assert.Contains("Supervisor review: PENDING", pending);

        iso.Db.SetSpecimenSupervisorReview("SR-8", "AB");
        var done = WithReviewRequired(true,
            () => reports.GeneratePreviewText(ReportType.ClinicalIdentification, "SR-8"));
        Assert.Contains("Supervisor reviewed by AB on", done);
    }
}
