using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class WorkloadTests
{
    private static Specimen Case(string acc, string received, string? tech, string? confirmed,
        string antibodies = "anti-E", string? reviewer = null, string? reviewed = null) => new()
    {
        AccessionNumber = acc, CreatedDate = received,
        FinalAntibodies = confirmed == null ? null : antibodies,
        IdentifiedBy = tech, IdentifiedAt = confirmed,
        ReviewedBy = reviewer, ReviewedAt = reviewed,
    };

    [Fact]
    public void CountsConfirmationsAndReviews_PerInitials_CaseInsensitive()
    {
        var specimens = new[]
        {
            Case("A", "2026-11-01", "ab", "2026-11-02 00:00", reviewer: "SU", reviewed: "2026-11-02 08:00"),
            Case("B", "2026-11-01", "AB", "2026-11-04 00:00", antibodies: "anti-E, anti-K"),
            Case("C", "2026-11-02", "CD", "2026-11-03 00:00", reviewer: "su", reviewed: "2026-11-03 09:00"),
        };

        var rows = WorkloadService.Compute(specimens, null, null);

        var ab = Assert.Single(rows, r => r.Initials == "AB");
        Assert.Equal(2, ab.Confirmed);
        Assert.Equal(1, ab.MultipleAntibodies);
        Assert.Equal(0, ab.Reviewed);
        Assert.Equal(2.0, ab.MedianConfirmDays); // 1 d and 3 d

        var su = Assert.Single(rows, r => r.Initials == "SU");
        Assert.Equal(0, su.Confirmed);
        Assert.Equal(2, su.Reviewed);
        Assert.Null(su.MedianConfirmDays);
        Assert.Equal("—", su.MedianConfirmDisplay);

        Assert.Equal(new[] { "AB", "SU", "CD" }, rows.Select(r => r.Initials)); // by total, then initials
    }

    [Fact]
    public void Range_UsesConfirmedAndReviewDates_NotReceivedDate()
    {
        var specimens = new[]
        {
            // Received before the range, confirmed and reviewed inside it.
            Case("A", "2026-10-20", "AB", "2026-11-05 10:00", reviewer: "SU", reviewed: "2026-11-06 10:00"),
            // Confirmed inside the range, reviewed after it.
            Case("B", "2026-11-01", "AB", "2026-11-07 10:00", reviewer: "SU", reviewed: "2026-11-20 10:00"),
            // Confirmed before the range.
            Case("C", "2026-10-01", "CD", "2026-10-02 10:00"),
        };

        var rows = WorkloadService.Compute(specimens, new DateTime(2026, 11, 1), new DateTime(2026, 11, 10));

        Assert.Equal(2, Assert.Single(rows, r => r.Initials == "AB").Confirmed);
        Assert.Equal(1, Assert.Single(rows, r => r.Initials == "SU").Reviewed);
        Assert.DoesNotContain(rows, r => r.Initials == "CD");
    }

    [Fact]
    public void ExcludesCancelled_Unconfirmed_AndBlankInitials()
    {
        var cancelled = Case("A", "2026-11-01", "AB", "2026-11-02 00:00");
        cancelled.CancelledAt = "2026-11-03 00:00";
        cancelled.CancelReason = "duplicate";
        var specimens = new[]
        {
            cancelled,
            Case("B", "2026-11-01", "AB", null),
            Case("C", "2026-11-01", "  ", "2026-11-02 00:00"),
        };

        Assert.Empty(WorkloadService.Compute(specimens, null, null));
    }

    [Fact]
    public void Names_ComeFromUserRegistry_WhenInitialsAreRegistered()
    {
        var users = new[]
        {
            new AppUser { Login = "jdoe", Initials = "AB", Name = "Jordan Doe", Role = UserRole.Technologist },
        };
        var rows = WorkloadService.Compute(new[]
        {
            Case("A", "2026-11-01", "ab", "2026-11-02 00:00"),
            Case("B", "2026-11-01", "ZZ", "2026-11-02 00:00"),
        }, null, null, users);

        Assert.Equal("Jordan Doe (AB)", Assert.Single(rows, r => r.Initials == "AB").Display);
        Assert.Equal("ZZ", Assert.Single(rows, r => r.Initials == "ZZ").Display);
    }

    [Fact]
    public void ConfirmationBeforeReceipt_IsCountedButLeftOutOfMedian()
    {
        var rows = WorkloadService.Compute(new[]
        {
            Case("A", "2026-11-05", "AB", "2026-11-02 00:00"),
        }, null, null);

        var ab = Assert.Single(rows);
        Assert.Equal(1, ab.Confirmed);
        Assert.Null(ab.MedianConfirmDays);
    }

    [Fact]
    public void CsvLines_QuoteNamesWithCommas()
    {
        var lines = AnalyticsViewModel.WorkloadCsvLines(new[]
        {
            new TechnologistWorkload { Initials = "AB", Name = "Doe, Jordan", Confirmed = 3, MultipleAntibodies = 1, Reviewed = 2, MedianConfirmDays = 1.5 },
        }).ToList();

        Assert.Contains("Initials,Name,Confirmed,Multiple antibodies,Supervisor reviews,Median received to confirmed (days)", lines);
        Assert.Contains("AB,\"Doe, Jordan\",3,1,2,1.50", lines);
    }

    [Fact]
    public void ReadsConfirmationsFromDatabase()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("WL-1", "serum", null);
        iso.Db.SetSpecimenFinalCall("WL-1", "anti-Fya; anti-Jkb", null, "TE");

        var rows = WorkloadService.Compute(iso.Db.GetAllSpecimens(), DateTime.Today.AddDays(-1), DateTime.Today, iso.Db.GetUsers());

        var te = Assert.Single(rows);
        Assert.Equal("TE", te.Initials);
        Assert.Equal(1, te.Confirmed);
        Assert.Equal(1, te.MultipleAntibodies);
    }
}
