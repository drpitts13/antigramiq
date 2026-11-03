using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class CancelInvestigationTests
{
    private static (IsolatedDatabase iso, int panelId) Setup(string acc)
    {
        var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen(acc, "serum", null);
        var panelId = iso.Db.AddPanel("Panel C", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "2+", "NT");
        return (iso, panelId);
    }

    [Fact]
    public void Cancel_RecordsReason_AndAudits()
    {
        var (iso, _) = Setup("CX-1");
        using (iso)
        {
            iso.Db.CancelInvestigation("CX-1", " mislabeled specimen ");
            var s = iso.Db.GetSpecimen("CX-1")!;
            Assert.True(s.IsCancelled);
            Assert.Equal("mislabeled specimen", s.CancelReason);
            var ev = Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "CX-1"), e => e.Action == "cancel_investigation");
            Assert.Equal("mislabeled specimen", ev.Reason);
        }
    }

    [Fact]
    public void Cancel_RequiresReason_RefusesConfirmedOrAlreadyCancelled()
    {
        var (iso, _) = Setup("CX-2");
        using (iso)
        {
            Assert.Throws<ArgumentException>(() => iso.Db.CancelInvestigation("CX-2", "  "));
            iso.Db.SetSpecimenFinalCall("CX-2", "anti-E", null, "DP");
            Assert.Throws<InvalidOperationException>(() => iso.Db.CancelInvestigation("CX-2", "x"));
            iso.Db.ClearSpecimenFinalCall("CX-2", "wrong");
            iso.Db.CancelInvestigation("CX-2", "hemolyzed");
            Assert.Throws<InvalidOperationException>(() => iso.Db.CancelInvestigation("CX-2", "again"));
        }
    }

    [Fact]
    public void Cancelled_IsLocked_AgainstReactionsPanelsAnalysisAndConfirm()
    {
        var (iso, panelId) = Setup("CX-3");
        using (iso)
        {
            iso.Db.CancelInvestigation("CX-3", "order cancelled");
            var ex = Assert.Throws<RecordLockedException>(() =>
                iso.Db.SaveReaction("CX-3", panelId, "2", "0", "0", "2+", "NT"));
            Assert.True(ex.Cancelled);
            Assert.Contains("cancelled", ex.Message);

            var other = iso.Db.AddPanel("Panel D", "LD", "V", 2, null, false);
            Assert.Throws<RecordLockedException>(() => iso.Db.LinkSpecimenPanel("CX-3", other));
            Assert.Throws<RecordLockedException>(() => iso.Db.SetSpecimenFinalCall("CX-3", "anti-E", null, "DP"));
            Assert.Throws<RecordLockedException>(() => iso.Analyzer.AnalyzeSpecimen("CX-3", updateDb: true));
            Assert.False(iso.Db.GetSpecimen("CX-3")!.HasFinalCall);
        }
    }

    [Fact]
    public void Reinstate_RequiresReason_UnlocksAndAudits()
    {
        var (iso, panelId) = Setup("CX-4");
        using (iso)
        {
            Assert.Throws<InvalidOperationException>(() => iso.Db.ReinstateInvestigation("CX-4", "not cancelled"));
            iso.Db.CancelInvestigation("CX-4", "mislabeled");
            Assert.Throws<ArgumentException>(() => iso.Db.ReinstateInvestigation("CX-4", ""));
            iso.Db.ReinstateInvestigation("CX-4", "label verified with collector");

            var s = iso.Db.GetSpecimen("CX-4")!;
            Assert.False(s.IsCancelled);
            Assert.Null(s.CancelReason);
            iso.Db.SaveReaction("CX-4", panelId, "2", "0", "0", "0", "2+"); // unlocked again
            var ev = Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "CX-4"), e => e.Action == "reinstate_investigation");
            Assert.Equal("label verified with collector", ev.Reason);
            Assert.Contains("mislabeled", ev.BeforeJson);
        }
    }

    [Fact]
    public void Status_Worklist_AndTimeline_ReflectCancellation()
    {
        var (iso, _) = Setup("CX-5");
        using (iso)
        {
            Assert.Contains(iso.Db.GetWorklistItems(), i => i.AccessionNumber == "CX-5");
            iso.Db.CancelInvestigation("CX-5", "hemolyzed specimen");

            var st = InvestigationStatusService.Compute(iso.Db, iso.Db.GetSpecimen("CX-5")!, requireReview: false);
            Assert.Equal(InvestigationStatusService.Cancelled, st.Label);
            Assert.Contains("hemolyzed specimen", st.NextAction);
            Assert.Contains("reinstate", st.NextAction);

            Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.AccessionNumber == "CX-5");

            iso.Db.ReinstateInvestigation("CX-5", "recollected");
            var whats = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("CX-5")!).Select(e => e.What).ToList();
            Assert.Contains("Investigation cancelled; reason: hemolyzed specimen", whats);
            Assert.Contains("Investigation reinstated; reason: recollected", whats);
            Assert.Contains(iso.Db.GetWorklistItems(), i => i.AccessionNumber == "CX-5");
        }
    }
}
