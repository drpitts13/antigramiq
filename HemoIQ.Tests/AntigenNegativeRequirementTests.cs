using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class AntigenNegativeRequirementTests
{
    [Theory]
    [InlineData("E, K", "E, K")]
    [InlineData("K-neg; E-negative", "E, K")]          // stored in panel order
    [InlineData("Fy(a-) Jk(b−)", "Jkb, Fya")]
    [InlineData("fya, jkb", "Jkb, Fya")]               // unambiguous case-insensitive match
    [InlineData("c e", "c, e")]                         // case decides c vs C, e vs E
    [InlineData("C, c", "C, c")]
    [InlineData("K K k", "K, k")]                       // duplicates collapse
    [InlineData("E−", "E")]
    public void Parse_NormalizesToCanonicalCodes(string text, string expected)
    {
        var r = AntigenNegativeRequirement.Parse(text);
        Assert.Empty(r.Unrecognized);
        Assert.Equal(expected, r.Canonical);
    }

    [Fact]
    public void Parse_ReportsUnrecognizedTokens_AndBlankIsNull()
    {
        var r = AntigenNegativeRequirement.Parse("E, irradiated, Xyz-");
        Assert.Equal(new[] { "E" }, r.Antigens);
        Assert.Equal(new[] { "irradiated", "Xyz-" }, r.Unrecognized);
        Assert.Null(AntigenNegativeRequirement.Parse("  ").Canonical);
    }

    [Fact]
    public void Display_UsesMinusAndPairedNotation()
    {
        Assert.Equal("E−, K−, Fy(a−)", AntigenNegativeRequirement.Display("E, K, Fya"));
        Assert.Equal("None recorded", AntigenNegativeRequirement.Display(null));
    }

    [Fact]
    public void SavePatient_StoresCanonical_Audits_AndRejectsUnknownAntigens()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.SavePatient(new Patient { Mrn = "AN-1", AntigenNegative = "k-neg, Fy(a-), E" });
        Assert.Equal("E, k, Fya", iso.Db.GetPatient("AN-1")!.AntigenNegative);

        var audit = Assert.Single(iso.Db.GetAuditEventsForEntity("patient", "AN-1"));
        Assert.Contains("E, k, Fya", audit.AfterJson);

        var ex = Assert.Throws<ArgumentException>(() =>
            iso.Db.SavePatient(new Patient { Mrn = "AN-1", AntigenNegative = "E, washed" }));
        Assert.Contains("washed is not a recognized antigen", ex.Message);
        Assert.Equal("E, k, Fya", iso.Db.GetPatient("AN-1")!.AntigenNegative);

        // Re-entering the same set in another form is not a change.
        iso.Db.SavePatient(new Patient { Mrn = "AN-1", AntigenNegative = "Fya E k" });
        Assert.Single(iso.Db.GetAuditEventsForEntity("patient", "AN-1"));
    }

    [Fact]
    public void Check_StructuredListSatisfiesRequirement_CaseSensitive()
    {
        var patient = new Patient { Mrn = "M", AntigenNegative = "c, Jka" };
        Assert.Empty(TransfusionRequirementCheck.MissingAntigenNegative(new[] { "anti-c", "anti-Jka" }, patient, "M"));

        var note = Assert.Single(TransfusionRequirementCheck.MissingAntigenNegative(new[] { "anti-C" }, patient, "M"));
        Assert.Contains("add C to Antigen-negative", note);
    }

    [Fact]
    public void Check_FreeTextOnly_PromptsToAddToStructuredList()
    {
        var patient = new Patient { Mrn = "M", TransfusionRequirements = "K-negative, irradiated" };
        var note = Assert.Single(TransfusionRequirementCheck.MissingAntigenNegative(new[] { "anti-K" }, patient, "M"));
        Assert.Contains("free-text transfusion requirements mention K-negative", note);
        Assert.Contains("not in the patient's structured Antigen-negative list", note);
    }

    [Fact]
    public void Worksheet_PrintsAntigenNegativeUnits()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("AN-2", "serum", null);
        iso.Db.UpdateSpecimenMrn("AN-2", "AN-2M");
        iso.Db.SavePatient(new Patient { Mrn = "AN-2M", Name = "Test", AntigenNegative = "Jkb, E" });

        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "AN-2");
        Assert.Contains("Antigen-negative units: E−, Jk(b−)", report);
    }
}
