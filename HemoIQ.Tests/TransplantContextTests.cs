using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

public class TransplantContextTests
{
    private static string Today(int days = 0) => DateTime.Today.AddDays(days).ToString("yyyy-MM-dd");

    [Fact]
    public void SavePatient_StoresTransplant_NormalizesType_AuditsChange_AndRejectsFutureDate()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.SavePatient(new Patient { Mrn = "TX-1", HpcTransplant = "Allo", HpcTransplantDate = Today(-30) });
        var p = iso.Db.GetPatient("TX-1")!;
        Assert.Equal(HpcTransplant.Allogeneic, p.HpcTransplant);
        Assert.Equal(Today(-30), p.HpcTransplantDate);
        Assert.Equal($"Allogeneic HPC transplant, {Today(-30)}", TransplantDisplay.Text(p));

        iso.Db.SavePatient(new Patient { Mrn = "TX-1", HpcTransplant = "allogeneic", HpcTransplantDate = Today(-30) });
        Assert.Single(iso.Db.GetAuditEventsForEntity("patient", "TX-1"));

        iso.Db.SavePatient(new Patient { Mrn = "TX-1", HpcTransplant = "none", HpcTransplantDate = Today(-30) });
        p = iso.Db.GetPatient("TX-1")!;
        Assert.Equal(HpcTransplant.None, p.HpcTransplant);
        Assert.Null(p.HpcTransplantDate);
        var update = iso.Db.GetAuditEventsForEntity("patient", "TX-1").Last();
        Assert.Contains("\"hpcTransplant\":\"allogeneic\"", update.BeforeJson);
        Assert.Contains("\"hpcTransplant\":\"none\"", update.AfterJson);

        Assert.Throws<ArgumentException>(() =>
            iso.Db.SavePatient(new Patient { Mrn = "TX-1", HpcTransplant = "allogeneic", HpcTransplantDate = Today(5) }));
        Assert.Equal("None recorded", TransplantDisplay.Text(iso.Db.GetPatient("TX-1")!));
    }

    [Fact]
    public void AffectsSpecimen_OnlyAllogeneic_OnOrAfterTransplant_UnknownDateAlwaysCounts()
    {
        var allo = new Patient { HpcTransplant = HpcTransplant.Allogeneic, HpcTransplantDate = "2026-05-01" };
        Assert.True(TransplantContext.AffectsSpecimen(allo, "2026-05-01"));
        Assert.True(TransplantContext.AffectsSpecimen(allo, "2026-09-10 08:30"));
        Assert.False(TransplantContext.AffectsSpecimen(allo, "2026-04-30"));
        Assert.True(TransplantContext.AffectsSpecimen(new Patient { HpcTransplant = HpcTransplant.Allogeneic }, "2020-01-01"));
        Assert.False(TransplantContext.AffectsSpecimen(new Patient { HpcTransplant = HpcTransplant.Autologous }, "2026-09-10"));
        Assert.False(TransplantContext.AffectsSpecimen(null, "2026-09-10"));
    }

    [Fact]
    public void AllogeneicTransplant_MakesPhenotypeAndGenotypeUninterpretable_WithExplanation()
    {
        var reliable = new TransfusionAssessment { PhenotypeUnreliable = false, Explanation = "No transfusion recorded." };
        var allo = new Patient { HpcTransplant = HpcTransplant.Allogeneic, HpcTransplantDate = "2026-05-01" };
        var assessed = TransplantContext.Apply(reliable, allo, "2026-09-10");
        Assert.True(assessed.PhenotypeUnreliable);
        Assert.True(assessed.GenotypeMayBeDonor);
        Assert.Contains("allogeneic HPC transplant is recorded on 2026-05-01", assessed.Explanation);

        var typing = PatientTypingParser.Parse("E-", null, null, "RHCE*ce/ce", assessed);
        var ev = PatientTypingParser.Evaluate(typing);
        Assert.DoesNotContain(ev, c => c.Antibody == "anti-E" && c.Kind is PatientTypingKind.Supporting or PatientTypingKind.Predicted);
        Assert.Contains(ev, c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Uninterpretable &&
                                 c.Explanation.Contains("allogeneic HPC transplant"));
        Assert.Contains(ev, c => c.Antibody == "anti-E" && c.Explanation.Contains("genotype from blood or marrow is the donor's"));
    }

    [Fact]
    public void BeforeTransplant_OrAutologous_LeavesTypingUsable()
    {
        var reliable = new TransfusionAssessment { PhenotypeUnreliable = false };
        var allo = new Patient { HpcTransplant = HpcTransplant.Allogeneic, HpcTransplantDate = "2026-05-01" };
        Assert.Same(reliable, TransplantContext.Apply(reliable, allo, "2026-04-01"));
        var auto = new Patient { HpcTransplant = HpcTransplant.Autologous, HpcTransplantDate = "2026-05-01" };
        var typing = PatientTypingParser.Parse("E-", null, null, null, TransplantContext.Apply(reliable, auto, "2026-09-10"));
        Assert.Contains(PatientTypingParser.Evaluate(typing), c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Supporting);
    }

    [Fact]
    public void Analyzer_UsesPatientTransplant_ForSpecimenTyping_AndReportShowsIt()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-S", "serum", null, phenotype: "E-");
        iso.Db.UpdateSpecimenMrn("TX-S", "MRN-TX");
        var panel = iso.Db.AddPanel("TX P", "TXL", "V", 2, null, false, startTypedNegative: true);
        foreach (var c in iso.Db.GetPanelCells(panel)) iso.Db.UpdatePanelCellAntigen(c.Id, "k", "+");
        iso.Db.LinkSpecimenPanel("TX-S", panel);
        iso.Db.SaveReaction("TX-S", panel, "1", "0", "0", "0", "2+");

        var before = iso.Analyzer.AnalyzeSpecimen("TX-S", updateDb: false);
        Assert.Contains(before.PatientTypingConsiderations, c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Supporting);

        iso.Db.SavePatient(new Patient { Mrn = "MRN-TX", HpcTransplant = HpcTransplant.Allogeneic, HpcTransplantDate = Today(-60) });
        var after = iso.Analyzer.AnalyzeSpecimen("TX-S", updateDb: false);
        Assert.True(after.PatientPhenotypeUnreliable);
        Assert.Contains("allogeneic HPC transplant", after.TransfusionContextNote);
        Assert.Contains(after.PatientTypingConsiderations, c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Uninterpretable);

        var report = iso.Reports.GeneratePreviewText(ReportType.ClinicalIdentification, "TX-S");
        Assert.Contains($"HPC transplant: Allogeneic HPC transplant, {Today(-60)}", report);
    }
}
