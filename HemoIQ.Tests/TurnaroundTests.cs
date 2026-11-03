using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class TurnaroundTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0);

    private static Specimen Confirmed(string acc, string received, string confirmed, string? reviewed = null) => new()
    {
        AccessionNumber = acc, CreatedDate = received, FinalAntibodies = "anti-E",
        IdentifiedAt = confirmed, ReviewedBy = reviewed == null ? null : "AB", ReviewedAt = reviewed,
    };

    [Fact]
    public void Stats_MedianAndNearestRank90th()
    {
        var odd = TurnaroundService.Stats(new double[] { 5, 1, 3 });
        Assert.Equal(3, odd.Median);
        Assert.Equal(5, odd.Percentile90);

        var ten = TurnaroundService.Stats(Enumerable.Range(1, 10).Select(i => (double)i).ToList());
        Assert.Equal(5.5, ten.Median);
        Assert.Equal(9, ten.Percentile90); // ceil(0.9*10) = 9th value

        var empty = TurnaroundService.Stats(Array.Empty<double>());
        Assert.Null(empty.Median);
        Assert.Equal("—", empty.MedianDisplay("d"));
    }

    [Fact]
    public void ReceivedToConfirmed_AndConfirmedToReviewed_UseRecordedTimes()
    {
        var specimens = new[]
        {
            Confirmed("A", "2026-11-01", "2026-11-02 00:00", reviewed: "2026-11-02 06:00"), // 1 d, 6 h
            Confirmed("B", "2026-11-01", "2026-11-04 00:00"),                               // 3 d, no review
            Confirmed("C", "2026-11-02", "2026-11-04 12:00", reviewed: "2026-11-05 00:00"), // 2.5 d, 12 h
        };
        var t = TurnaroundService.Compute(specimens, null, null, 3, Now);
        Assert.Equal(3, t.ReceivedToConfirmed.Count);
        Assert.Equal(2.5, t.ReceivedToConfirmed.Median);
        Assert.Equal(3, t.ReceivedToConfirmed.Percentile90);
        Assert.Equal(2, t.ConfirmedToReviewed.Count);
        Assert.Equal(9, t.ConfirmedToReviewed.Median);
    }

    [Fact]
    public void CancelledAndOutOfRange_AreExcluded()
    {
        var cancelled = Confirmed("X", "2026-11-01", "2026-11-09 00:00");
        cancelled.CancelledAt = "2026-11-02 08:00";
        var specimens = new[]
        {
            cancelled,
            Confirmed("OLD", "2026-01-01", "2026-01-30 00:00"),
            Confirmed("IN", "2026-11-03", "2026-11-04 00:00"),
        };
        var t = TurnaroundService.Compute(specimens, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), 3, Now);
        Assert.Equal(1, t.ReceivedToConfirmed.Count);
        Assert.Equal(1, t.ReceivedToConfirmed.Median);
    }

    [Fact]
    public void Overdue_ListsOnlyActiveOpenOlderThanThreshold_OldestFirst()
    {
        var cancelledOpen = new Specimen { AccessionNumber = "CX", CreatedDate = "2026-11-01", CancelledAt = "2026-11-02" };
        var inactiveOpen = new Specimen { AccessionNumber = "IN", CreatedDate = "2026-11-01", IsActive = false };
        var specimens = new[]
        {
            new Specimen { AccessionNumber = "OPEN-9", CreatedDate = "2026-11-01" },  // 9 days
            new Specimen { AccessionNumber = "OPEN-5", CreatedDate = "2026-11-05" },  // 5 days
            new Specimen { AccessionNumber = "OPEN-3", CreatedDate = "2026-11-07" },  // 3 days: not over
            Confirmed("DONE", "2026-11-01", "2026-11-09 00:00"),
            cancelledOpen, inactiveOpen,
        };
        var t = TurnaroundService.Compute(specimens, null, null, 3, Now, s => "status-" + s.AccessionNumber);
        Assert.Equal(new[] { "OPEN-9", "OPEN-5" }, t.Overdue.Select(o => o.Accession));
        Assert.Equal(9, t.Overdue[0].AgeDays);
        Assert.Equal("status-OPEN-9", t.Overdue[0].Status);
    }

    [Fact]
    public void ThresholdSetting_IsClamped()
    {
        var s = new LabSettings { TurnaroundThresholdDays = 0 };
        s.Clamp();
        Assert.Equal(1, s.TurnaroundThresholdDays);
        s.TurnaroundThresholdDays = 500;
        s.Clamp();
        Assert.Equal(60, s.TurnaroundThresholdDays);
    }

    [Fact]
    public void AnalyticsViewModel_ComputesFromDatabase_AndCsvIncludesTurnaround()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TA-1", "serum", null, createdDate: DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"));
        iso.Db.SetSpecimenFinalCall("TA-1", "anti-K", null, "DP");
        iso.Db.AddSpecimen("TA-2", "serum", null, createdDate: DateTime.Today.AddDays(-20).ToString("yyyy-MM-dd"));

        var previous = AppSettings.Current.TurnaroundThresholdDays;
        try
        {
            AppSettings.Current.TurnaroundThresholdDays = 3;
            var vm = new AnalyticsViewModel(iso.Db);
            Assert.Equal(1, vm.Turnaround.ReceivedToConfirmed.Count);
            var overdue = Assert.Single(vm.Overdue);
            Assert.Equal("TA-2", overdue.Accession);
            Assert.Equal(InvestigationStatusService.AwaitingPanel, overdue.Status);
            Assert.Equal("1 open > 3 d", vm.OverdueLabel);

            var csv = AnalyticsViewModel.TurnaroundCsvLines(vm.Turnaround).ToList();
            Assert.Contains(csv, l => l.StartsWith("Received to confirmed,1,"));
            Assert.Contains(csv, l => l.StartsWith("TA-2,"));
        }
        finally
        {
            AppSettings.Current.TurnaroundThresholdDays = previous;
        }
    }
}
