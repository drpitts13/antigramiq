using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.Tests;

public class AnalyticsDrillDownTests
{
    private static Specimen S(string acc, string? final, string? at, string? by = "TE",
        string created = "2026-10-01", string? location = null, string? reviewedBy = null, string? reviewedAt = null,
        string? cancelled = null) => new()
    {
        AccessionNumber = acc, FinalAntibodies = final, IdentifiedAt = at, IdentifiedBy = by,
        CreatedDate = created, OrderingLocation = location, ReviewedBy = reviewedBy, ReviewedAt = reviewedAt,
        CancelledAt = cancelled, IsActive = true,
    };

    private static readonly Specimen[] Data =
    {
        S("A1", "anti-E", "2026-10-05 10:00"),
        S("A2", "Anti-E; anti-K", "2026-10-07 09:00", by: "SU", reviewedBy: "TE", reviewedAt: "2026-10-08 12:00"),
        S("A3", "anti-e", "2026-10-06 08:00", location: "ICU"),
        S("A4", "anti-E", "2025-01-01 08:00"),
        S("A5", null, null, location: "icu "),
        S("A6", "anti-E", "2026-10-09 08:00", cancelled: "2026-10-10"),
    };

    private static readonly DateTime From = new(2026, 10, 1), To = new(2026, 10, 31);

    [Fact]
    public void FrequencyTable_KeepsAntiEAndAntiESeparate_PrefixCaseStillMerges()
    {
        var result = AntibodyAnalyticsService.Compute(Data, From, To);
        var e = Assert.Single(result.Rows, r => r.Antibody == "anti-E");
        Assert.Equal(3, e.Count);
        Assert.Equal(1, Assert.Single(result.Rows, r => r.Antibody == "anti-e").Count);
        Assert.Equal(1, Assert.Single(result.Rows, r => r.Antibody == "anti-K").Count);
    }

    [Fact]
    public void AntibodyDrillDown_ListsExactlyTheCountedInvestigations_NewestFirst()
    {
        var count = AntibodyAnalyticsService.Compute(Data, From, To).Rows.Single(r => r.Antibody == "anti-E").Count;
        var cases = AntibodyAnalyticsService.CasesForAntibody(Data, From, To, "anti-E");
        Assert.Equal(count, cases.Count);
        Assert.Equal(new[] { "A6", "A2", "A1" }, cases.Select(c => c.Accession));
        Assert.Equal("2026-10-07", cases[1].Date);
        Assert.Equal("Confirmed by SU", cases[1].Detail);
        Assert.Equal(new[] { "A3" }, AntibodyAnalyticsService.CasesForAntibody(Data, From, To, "anti-e").Select(c => c.Accession));
        Assert.Contains(AntibodyAnalyticsService.CasesForAntibody(Data, null, null, "anti-E"), c => c.Accession == "A4");
    }

    [Fact]
    public void TechnologistDrillDown_MatchesWorkloadCounts_ExcludesCancelled()
    {
        var row = WorkloadService.Compute(Data, From, To).Single(w => w.Initials == "TE");
        var cases = WorkloadService.CasesForTechnologist(Data, From, To, "te");
        Assert.Equal(row.Confirmed, cases.Count(c => c.Detail == "Confirmed"));
        Assert.Equal(row.Reviewed, cases.Count(c => c.Detail == "Supervisor review"));
        Assert.DoesNotContain(cases, c => c.Accession == "A6");
        Assert.Equal("A2", cases[0].Accession);
    }

    [Fact]
    public void LocationDrillDown_MatchesLocationRow_IgnoringCaseAndSpaces()
    {
        var row = WorkloadService.ComputeByLocation(Data, From, To).Single(w => w.Location.Equals("ICU", StringComparison.OrdinalIgnoreCase));
        var cases = WorkloadService.CasesForLocation(Data, From, To, row.Location);
        Assert.Equal(row.Received, cases.Count);
        Assert.Equal(new[] { "Confirmed", "Open" }, cases.Select(c => c.Detail).OrderBy(d => d));
        Assert.Contains(WorkloadService.CasesForLocation(Data, From, To, WorkloadService.NoLocation), c => c.Accession == "A1");
    }
}
