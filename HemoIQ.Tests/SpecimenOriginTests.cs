using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using HemoIQ.ViewModels;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class SpecimenOriginTests
{
    [Fact]
    public void Origin_IsStoredTrimmed_Audited_AndOnTimeline()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("OR-1", "serum", null);
        iso.Db.UpdateSpecimenOrigin("OR-1", "  J  Smith ", "4 West", " ICU ", "Dr. Lee");

        var s = iso.Db.GetSpecimen("OR-1")!;
        Assert.Equal("J Smith", s.Collector);
        Assert.Equal("4 West", s.CollectionLocation);
        Assert.Equal("ICU", s.OrderingLocation);
        Assert.Equal("Dr. Lee", s.OrderingProvider);
        Assert.Equal("Collected by J Smith at 4 West; Ordered by Dr. Lee from ICU", s.OriginDisplay);

        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "OR-1"), e => e.Action == "update_origin");
        Assert.Contains("ICU", audit.AfterJson);
        Assert.Contains(InvestigationTimeline.Build(iso.Db, s), e => e.What == "Origin recorded: " + s.OriginDisplay);
    }

    [Fact]
    public void UnchangedOrigin_WritesNoAudit_AndBlankClears()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("OR-2", "serum", null);
        iso.Db.UpdateSpecimenOrigin("OR-2", null, " ", "", null);
        Assert.DoesNotContain(iso.Db.GetAuditEventsForEntity("specimen", "OR-2"), e => e.Action == "update_origin");

        iso.Db.UpdateSpecimenOrigin("OR-2", "AB", null, "ED", null);
        iso.Db.UpdateSpecimenOrigin("OR-2", "AB", null, "ED", null);
        iso.Db.UpdateSpecimenOrigin("OR-2", null, null, null, null);
        var s = iso.Db.GetSpecimen("OR-2")!;
        Assert.Null(s.Collector);
        Assert.Null(s.OrderingLocation);
        Assert.Equal(string.Empty, s.OriginDisplay);
        Assert.Equal(2, iso.Db.GetAuditEventsForEntity("specimen", "OR-2").Count(e => e.Action == "update_origin"));
        Assert.Contains(InvestigationTimeline.Build(iso.Db, s), e => e.What == "Origin recorded: cleared");
    }

    [Fact]
    public void TooLongValue_IsRejected_AndLockedAfterFinalCall()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("OR-3", "serum", null);
        var ex = Assert.Throws<ArgumentException>(() =>
            iso.Db.UpdateSpecimenOrigin("OR-3", null, null, new string('x', 129), null));
        Assert.Contains("Ordering location is longer than 128", ex.Message);

        iso.Db.SetSpecimenFinalCall("OR-3", "anti-E", null, "TE");
        Assert.Throws<RecordLockedException>(() => iso.Db.UpdateSpecimenOrigin("OR-3", "AB", null, null, null));
    }

    [Fact]
    public void Search_FindsByLocationAndProvider_AndWorksheetPrintsOrigin()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("OR-4", "serum", null);
        iso.Db.AddSpecimen("OR-5", "serum", null);
        iso.Db.UpdateSpecimenOrigin("OR-4", "PH", "L&D", "Labor and Delivery", "Dr. Okafor");

        Assert.Equal("OR-4", Assert.Single(InvestigationSearch.Search(iso.Db, "okafor")).Accession);
        Assert.Equal("OR-4", Assert.Single(InvestigationSearch.Search(iso.Db, "labor delivery")).Accession);

        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "OR-4");
        Assert.Contains("Origin: Collected by PH at L&D; Ordered by Dr. Okafor from Labor and Delivery", report);
        var other = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "OR-5");
        Assert.Contains("Origin: not recorded", other);
    }

    [Fact]
    public void Database_RejectsOverlongLocation_EvenOutsideTheService()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("OR-6", "serum", null);
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={iso.DbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE specimens SET ordering_location = '{new string('x', 200)}' WHERE accession_number = 'OR-6'";
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
        Assert.Contains("CHECK", ex.Message);
    }

    private static Specimen Received(string acc, string received, string? location, string? confirmed = null) => new()
    {
        AccessionNumber = acc, CreatedDate = received, OrderingLocation = location,
        FinalAntibodies = confirmed == null ? null : "anti-E", IdentifiedBy = confirmed == null ? null : "AB",
        IdentifiedAt = confirmed,
    };

    [Fact]
    public void ByLocation_GroupsCaseInsensitively_CountsOpen_AndPutsUnrecordedLast()
    {
        var cancelled = Received("X", "2026-11-02", "ICU");
        cancelled.CancelledAt = "2026-11-03 00:00";
        var specimens = new[]
        {
            Received("A", "2026-11-01", "ICU", "2026-11-02 00:00"),
            Received("B", "2026-11-01", "icu ", "2026-11-04 00:00"),
            Received("C", "2026-11-02", "ICU"),
            Received("D", "2026-11-02", null),
            Received("E", "2026-11-02", "ED"),
            Received("F", "2026-11-02", "ED"),
            Received("G", "2026-11-02", "ED"),
            Received("H", "2026-11-02", "ED"),
            Received("Old", "2026-09-01", "ED"),
            cancelled,
        };

        var rows = WorkloadService.ComputeByLocation(specimens, new DateTime(2026, 11, 1), new DateTime(2026, 11, 10));

        Assert.Equal(new[] { "ED", "ICU", WorkloadService.NoLocation }, rows.Select(r => r.Location));
        var icu = rows[1];
        Assert.Equal(3, icu.Received);
        Assert.Equal(2, icu.Confirmed);
        Assert.Equal(1, icu.Open);
        Assert.Equal(2.0, icu.MedianConfirmDays);
        Assert.Equal(4, rows[0].Received);
        Assert.Equal("—", rows[0].MedianConfirmDisplay);
    }

    [Fact]
    public void LocationCsv_QuotesCommas()
    {
        var lines = AnalyticsViewModel.LocationCsvLines(new[]
        {
            new LocationWorkload { Location = "Clinic, North", Received = 2, Confirmed = 1, Open = 1, MedianConfirmDays = 0.5 },
        }).ToList();
        Assert.Contains("\"Clinic, North\",2,1,1,0.50", lines);
    }
}
