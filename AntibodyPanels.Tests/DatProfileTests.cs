using AntibodyPanels.Models;
using AntibodyPanels.Services;
using AntibodyPanels.Tests.Infrastructure;

namespace AntibodyPanels.Tests;

[Collection("PersistentDatabase")]
public class DatProfileTests
{
    // ── Profile rules ──────────────────────────────────────────────────

    [Fact]
    public void IgGPositive_WithPolyNotTested_IsPositive()
    {
        var dat = new DatProfile { Polyspecific = "NT", IgG = "2+", C3 = "Negative" };
        Assert.True(dat.IsPositive);
        Assert.False(dat.IsNegative);
        Assert.Equal("IgG 2+, C3 Negative", dat.Describe());
    }

    [Fact]
    public void AllRecordedNegative_IsNegative()
    {
        var dat = new DatProfile { Polyspecific = "Negative", IgG = "Negative", C3 = null };
        Assert.True(dat.IsNegative);
        Assert.False(dat.IsPositive);
    }

    [Fact]
    public void NothingRecorded_IsNeitherPositiveNorNegative()
    {
        var dat = new DatProfile { Polyspecific = "NT" };
        Assert.False(dat.IsPositive);
        Assert.False(dat.IsNegative);
        Assert.Equal("NT", dat.Describe());
    }

    [Fact]
    public void PolyOnly_KeepsLegacyDescription()
    {
        Assert.Equal("2+", DatProfile.FromPolyspecific("2+").Describe());
        Assert.False(DatProfile.FromPolyspecific("2+").HasMonospecific);
    }

    [Fact]
    public void C3Only_RequiresIgGRecordedNegative()
    {
        Assert.True(new DatProfile { IgG = "Negative", C3 = "1+" }.C3Only);
        Assert.False(new DatProfile { C3 = "1+" }.C3Only); // IgG not done: not called C3-only
        Assert.False(new DatProfile { IgG = "1+", C3 = "1+" }.C3Only);
    }

    [Fact]
    public void MixedFieldResults_NamesEachMfResult()
    {
        var dat = new DatProfile { Polyspecific = "MF", IgG = "MF", C3 = "Negative" };
        Assert.Equal(new[] { "polyspecific", "IgG" }, dat.MixedFieldResults);
        Assert.True(dat.IsPositive);
    }

    // ── Pattern classifier ─────────────────────────────────────────────

    private static List<PatternCellObservation> PanreactiveNoAc() =>
        Enumerable.Range(1, 5).Select(i => new PatternCellObservation(
            i.ToString(), Evaluated: true, AnyReactive: true, ColdPhaseReactive: false,
            IatPhaseReactive: true, IatStrength: 2)).ToList();

    [Fact]
    public void Classifier_IgGOnlyPositive_FavorsAutoantibody_AndNamesSplit()
    {
        var notes = ReactionPatternClassifier.Classify(PanreactiveNoAc(),
            new DatProfile { Polyspecific = "NT", IgG = "2+", C3 = "Negative" });
        var auto = Assert.Single(notes, n => n.Kind == ReactionPatternClassifier.Autoantibody);
        Assert.Contains("DAT is IgG 2+, C3 Negative", auto.Explanation);
        Assert.DoesNotContain("Only C3", auto.Explanation);
    }

    [Fact]
    public void Classifier_C3Only_AddsColdOrComplementCaveat()
    {
        var notes = ReactionPatternClassifier.Classify(PanreactiveNoAc(),
            new DatProfile { IgG = "Negative", C3 = "1+" });
        var auto = Assert.Single(notes, n => n.Kind == ReactionPatternClassifier.Autoantibody);
        Assert.Contains("Only C3 is reactive", auto.Explanation);
        Assert.Contains("prewarm", auto.Explanation);
    }

    [Fact]
    public void Classifier_MonospecificNegative_FavorsHighPrevalence_AndNamesResults()
    {
        var notes = ReactionPatternClassifier.Classify(PanreactiveNoAc(),
            new DatProfile { IgG = "Negative", C3 = "Negative" });
        var hfa = Assert.Single(notes, n => n.Kind == ReactionPatternClassifier.HighPrevalence);
        Assert.Contains("DAT is negative (IgG Negative, C3 Negative)", hfa.Explanation);
    }

    // ── Mixed-field DAT ────────────────────────────────────────────────

    [Fact]
    public void MixedFieldIgG_AfterRecentTransfusion_GivesDonorCellNote()
    {
        var t = TransfusionContext.Assess("transfused", "2026-10-25", "2026-11-03", 90, null, null);
        var note = Assert.Single(TransfusionContext.DescribeMixedFieldDat(new DatProfile { IgG = "MF" }, t));
        Assert.Contains("DAT IgG is mixed-field", note);
        Assert.Contains("surviving donor cells", note);
    }

    [Fact]
    public void NonMixedFieldDat_GivesNoNote()
    {
        var t = TransfusionContext.Assess("transfused", "2026-10-25", "2026-11-03", 90, null, null);
        Assert.Empty(TransfusionContext.DescribeMixedFieldDat(new DatProfile { IgG = "2+", C3 = "1+" }, t));
    }

    // ── Persistence ────────────────────────────────────────────────────

    [Fact]
    public void UpdateDatSplit_PersistsAndAudits()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("DAT-1", "serum", null, datResult: "1+");
        iso.Db.UpdateSpecimenDatSplit("DAT-1", "2+", "negative");

        var s = iso.Db.GetSpecimen("DAT-1")!;
        Assert.Equal("2+", s.DatIgg);
        Assert.Equal("Negative", s.DatC3); // normalized to the listed grade
        Assert.Equal("polyspecific 1+, IgG 2+, C3 Negative", s.DatDisplay);
        var ev = Assert.Single(iso.Db.GetAuditEvents(), e => e.Action == "update_dat");
        var after = System.Text.Json.JsonDocument.Parse(ev.AfterJson!).RootElement;
        Assert.Equal("2+", after.GetProperty("igg").GetString());
        Assert.Equal("Negative", after.GetProperty("c3").GetString());
        Assert.Contains("null", ev.BeforeJson);
    }

    [Fact]
    public void UpdateDatSplit_NtClears_AndUnchangedWritesNoAudit()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("DAT-2", "serum", null);
        iso.Db.UpdateSpecimenDatSplit("DAT-2", "NT", null);
        Assert.DoesNotContain(iso.Db.GetAuditEvents(), e => e.Action == "update_dat");
        Assert.Null(iso.Db.GetSpecimen("DAT-2")!.DatIgg);
    }

    [Fact]
    public void UpdateDatSplit_RejectsUnknownGrade_AndIsLockedAfterFinalCall()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("DAT-3", "serum", null);
        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenDatSplit("DAT-3", "5+", null));
        iso.Db.SetSpecimenFinalCall("DAT-3", "anti-E", null, "DP");
        Assert.Throws<RecordLockedException>(() => iso.Db.UpdateSpecimenDatSplit("DAT-3", "1+", null));
    }

    [Fact]
    public void Analyzer_MixedFieldIgGDat_AddsNote_AndReportShowsSplit()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("DAT-AN", "serum", null, phenotype: "E+", createdDate: "2026-11-03");
        var panelId = iso.Db.AddPanel("P", "L-DAT", "V", 2, null, false);
        iso.Db.LinkSpecimenPanel("DAT-AN", panelId);
        iso.Db.SaveReaction("DAT-AN", panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction("DAT-AN", panelId, "2", "0", "0", "0", "2+");
        iso.Db.UpdateSpecimenTransfusion("DAT-AN", "transfused", "2026-10-28");
        iso.Db.UpdateSpecimenDatSplit("DAT-AN", "MF", "Negative");

        var result = iso.Analyzer.AnalyzeSpecimen("DAT-AN", updateDb: false);
        Assert.Contains(result.SpecialReactionNotes, n => n.Contains("DAT IgG is mixed-field"));

        var report = new ReportService(iso.Db).GeneratePreviewText(ReportType.ClinicalIdentification, "DAT-AN");
        Assert.Contains("DAT: IgG MF, C3 Negative", report);
    }
}
