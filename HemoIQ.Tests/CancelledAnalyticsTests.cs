using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class CancelledAnalyticsTests
{
    /// <summary>Marks a specimen cancelled directly, as older versions or imported data could leave it.</summary>
    private static void CancelLegacy(IsolatedDatabase iso, string acc)
    {
        using var conn = new SqliteConnection($"Data Source={iso.DbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE specimens SET cancelled_at = '2026-10-20 08:00', cancel_reason = 'legacy' WHERE accession_number = $a";
        cmd.Parameters.AddWithValue("$a", acc);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void CancelledWithFinalId_NotCountedInAnalytics_AndListedOnWorklist_UntilResolved()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("CX-1", "serum", null);
        iso.Db.AddSpecimen("CX-2", "serum", null);
        iso.Db.SetSpecimenFinalCall("CX-1", "anti-K", null, "TE");
        iso.Db.SetSpecimenFinalCall("CX-2", "anti-K", null, "TE");
        CancelLegacy(iso, "CX-2");

        var specimens = iso.Db.GetAllSpecimens();
        var row = Assert.Single(AntibodyAnalyticsService.Compute(specimens, null, null).Rows, r => r.Antibody == "anti-K");
        Assert.Equal(1, row.Count);
        Assert.Equal(new[] { "CX-1" }, AntibodyAnalyticsService.CasesForAntibody(specimens, null, null, "anti-K").Select(c => c.Accession));

        var item = Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.InconsistentStatus);
        Assert.Equal("CX-2", item.Title);
        Assert.Contains("still holds confirmed identification anti-K", item.Detail);

        iso.Db.ClearSpecimenFinalCall("CX-2", "entered on the wrong accession");
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.InconsistentStatus);
    }

    [Fact]
    public void ReinstatingKeepsTheIdentification_AndResolvesTheItem()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("CX-3", "serum", null);
        iso.Db.SetSpecimenFinalCall("CX-3", "anti-E", null, "TE");
        CancelLegacy(iso, "CX-3");
        Assert.Single(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.InconsistentStatus);

        iso.Db.ReinstateInvestigation("CX-3", "cancelled in error");
        Assert.DoesNotContain(iso.Db.GetWorklistItems(), i => i.Kind == WorklistKind.InconsistentStatus);
        Assert.Equal(1, AntibodyAnalyticsService.Compute(iso.Db.GetAllSpecimens(), null, null).ConfirmedSpecimens);
    }
}
