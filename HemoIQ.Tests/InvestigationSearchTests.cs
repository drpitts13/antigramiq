using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class InvestigationSearchTests
{
    private static IsolatedDatabase Seed()
    {
        var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("ACC-100", "serum", null, createdDate: "2026-01-10");
        iso.Db.UpdateSpecimenMrn("ACC-100", "MRN-1234");
        iso.Db.SetSpecimenFinalCall("ACC-100", "anti-Jka", null, "DP");

        iso.Db.AddSpecimen("ACC-200", "serum", null, createdDate: "2026-10-01");
        iso.Db.UpdateSpecimenMrn("ACC-200", "MRN-1234");

        iso.Db.AddSpecimen("ACC-300", "plasma", null, previousAntibodies: "anti-K", createdDate: "2026-06-15");
        iso.Db.UpdateSpecimenMrn("ACC-300", "MRN-9999");
        iso.Db.SetSpecimenFinalCall("ACC-300", "anti-E", null, "RT");

        iso.Db.SavePatient(new Patient { Mrn = "MRN-1234", Name = "Doe, Pat", DateOfBirth = "1980-02-03" });
        iso.Db.SavePatient(new Patient { Mrn = "MRN-9999", Name = "Roe, Sam" });
        return iso;
    }

    private static List<string> Accessions(IEnumerable<InvestigationSearchRow> rows) =>
        rows.Select(r => r.Accession).ToList();

    [Fact]
    public void PartialMrn_FindsAllOfThatPatientsSpecimens_NewestFirst()
    {
        using var iso = Seed();
        var rows = InvestigationSearch.Search(iso.Db, "mrn-12");
        Assert.Equal(new[] { "ACC-200", "ACC-100" }, Accessions(rows));
        Assert.All(rows, r => Assert.Equal("Doe, Pat", r.Patient));
    }

    [Fact]
    public void PatientName_IsCaseInsensitive_AndRowsCarryStatusAndDob()
    {
        using var iso = Seed();
        var rows = InvestigationSearch.Search(iso.Db, "DOE");
        Assert.Equal(2, rows.Count);
        var done = rows.Single(r => r.Accession == "ACC-100");
        Assert.Equal(InvestigationStatusService.Completed, done.Status);
        Assert.Equal("1980-02-03", done.DateOfBirth);
        Assert.Equal(InvestigationStatusService.AwaitingPanel, rows.Single(r => r.Accession == "ACC-200").Status);
    }

    [Fact]
    public void FinalAntibody_Technologist_AndPreviousAntibodies_AreSearchable()
    {
        using var iso = Seed();
        Assert.Equal(new[] { "ACC-100" }, Accessions(InvestigationSearch.Search(iso.Db, "anti-Jka")));
        Assert.Equal(new[] { "ACC-300" }, Accessions(InvestigationSearch.Search(iso.Db, "RT")));
        Assert.Equal(new[] { "ACC-300" }, Accessions(InvestigationSearch.Search(iso.Db, "anti-K")));
    }

    [Fact]
    public void EveryWordMustMatch()
    {
        using var iso = Seed();
        Assert.Equal(new[] { "ACC-100" }, Accessions(InvestigationSearch.Search(iso.Db, "doe jka")));
        Assert.Empty(InvestigationSearch.Search(iso.Db, "roe jka"));
    }

    [Fact]
    public void DateRange_IsInclusive_AndCanBeUsedAlone()
    {
        using var iso = Seed();
        var mid = InvestigationSearch.Search(iso.Db, null, new DateTime(2026, 6, 15), new DateTime(2026, 10, 1));
        Assert.Equal(new[] { "ACC-200", "ACC-300" }, Accessions(mid));
        var withText = InvestigationSearch.Search(iso.Db, "doe", new DateTime(2026, 2, 1), null);
        Assert.Equal(new[] { "ACC-200" }, Accessions(withText));
    }

    [Fact]
    public void NoMatch_ReturnsEmpty_AndSpecimenWithoutPatientRecordStillFound()
    {
        using var iso = Seed();
        Assert.Empty(InvestigationSearch.Search(iso.Db, "nobody-here"));
        iso.Db.AddSpecimen("ACC-400", "serum", null, createdDate: "2026-11-01");
        var row = Assert.Single(InvestigationSearch.Search(iso.Db, "ACC-400"));
        Assert.Equal(string.Empty, row.Mrn);
        Assert.Equal(string.Empty, row.Patient);
    }

    [Fact]
    public void GetAllPatients_ReturnsSavedRecords()
    {
        using var iso = Seed();
        Assert.Equal(new[] { "MRN-1234", "MRN-9999" }, iso.Db.GetAllPatients().Select(p => p.Mrn));
    }
}
