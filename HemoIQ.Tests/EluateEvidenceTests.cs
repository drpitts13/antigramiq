using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class EluateEvidenceTests
{
    /// <summary>Source serum with two reactive cells (so analysis runs) and an eluate graded per <paramref name="eluateAhg"/>.</summary>
    private static IsolatedDatabase Setup(string src, string elu, params string[] eluateAhg)
    {
        var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen(src, "serum", null, createdDate: "2026-11-03");
        var sp = iso.Db.AddPanel("Src panel", "L-" + src, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(src, sp);
        iso.Db.SaveReaction(src, sp, "1", "0", "0", "2+", "NT");
        iso.Db.SaveReaction(src, sp, "2", "0", "0", "0", "2+");

        iso.Db.AddSpecimen(elu, "eluate", null, createdDate: "2026-11-03");
        iso.Db.SetEluateSource(elu, src);
        if (eluateAhg.Length > 0)
        {
            var ep = iso.Db.AddPanel("Eluate panel", "L-" + elu, "V", eluateAhg.Length, null, false);
            iso.Db.LinkSpecimenPanel(elu, ep);
            for (int i = 0; i < eluateAhg.Length; i++)
                iso.Db.SaveReaction(elu, ep, (i + 1).ToString(), "NT", "NT", eluateAhg[i], eluateAhg[i] == "0" ? "2+" : "NT");
        }
        return iso;
    }

    private static List<string> NotesFor(IsolatedDatabase iso, string src) =>
        iso.Analyzer.AnalyzeSpecimen(src, updateDb: false).SpecialReactionNotes
            .Where(n => n.StartsWith("Eluate ")).ToList();

    // ── Link validation and audit ───────────────────────────────────────

    [Fact]
    public void Link_IsAudited_AndShownBothWays()
    {
        using var iso = Setup("EL-S1", "EL-E1");
        Assert.Equal("EL-S1", iso.Db.GetSpecimen("EL-E1")!.SourceAccession);
        Assert.Equal(new[] { "EL-E1" }, iso.Db.GetEluatesFor("EL-S1").Select(e => e.AccessionNumber));
        Assert.Single(iso.Db.GetAuditEventsForEntity("specimen", "EL-E1"), e => e.Action == "link_eluate");
        Assert.Contains(InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("EL-E1")!),
            e => e.What == "Linked as eluate of EL-S1");

        iso.Db.SetEluateSource("EL-E1", null);
        Assert.Empty(iso.Db.GetEluatesFor("EL-S1"));
    }

    [Fact]
    public void Link_RefusesNonEluate_Self_MissingSource_AndEluateSource()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("S", "serum", null);
        iso.Db.AddSpecimen("E1", "eluate", null);
        iso.Db.AddSpecimen("E2", "eluate", null);
        Assert.Throws<ArgumentException>(() => iso.Db.SetEluateSource("S", "E1"));
        Assert.Throws<ArgumentException>(() => iso.Db.SetEluateSource("E1", "E1"));
        Assert.Throws<ArgumentException>(() => iso.Db.SetEluateSource("E1", "NOPE"));
        Assert.Throws<ArgumentException>(() => iso.Db.SetEluateSource("E1", "E2"));
        Assert.Null(iso.Db.GetSpecimen("E1")!.SourceAccession);
    }

    // ── Evidence notes ──────────────────────────────────────────────────

    [Fact]
    public void NoReactionsYet_SaysSo()
    {
        using var iso = Setup("EL-S2", "EL-E2");
        Assert.Contains("no reactions entered yet", Assert.Single(NotesFor(iso, "EL-S2")));
    }

    [Fact]
    public void Specificity_NotSuspectedInSerum_AfterRecentTransfusion_NamesBothConcerns()
    {
        using var iso = Setup("EL-S3", "EL-E3", "2+", "0", "2+", "0");
        iso.Db.AddSpecimenAntibody("EL-E3", "anti-Jka", 0.9);
        iso.Db.UpdateSpecimenTransfusion("EL-S3", "transfused", "2026-10-28");

        var note = Assert.Single(NotesFor(iso, "EL-S3"));
        Assert.Contains("shows anti-Jka (suspected); 2 of 4 evaluated cells reactive", note);
        Assert.Contains("coating transfused donor cells", note);
        Assert.Contains("anti-Jka is in the eluate but not suspected in serum EL-S3", note);
        Assert.Contains("not a diagnosis", note);
    }

    [Fact]
    public void ConfirmedEluateCall_IsPreferredOverSuspected()
    {
        using var iso = Setup("EL-S4", "EL-E4", "2+", "0", "0");
        iso.Db.AddSpecimenAntibody("EL-E4", "anti-K", 0.8);
        iso.Db.SetSpecimenFinalCall("EL-E4", "anti-E", null, "DP");
        var note = Assert.Single(NotesFor(iso, "EL-S4"));
        Assert.Contains("anti-E (confirmed)", note);
        Assert.DoesNotContain("anti-K", note);
    }

    [Fact]
    public void PanreactiveEluate_SupportsAutoantibody()
    {
        using var iso = Setup("EL-S5", "EL-E5", "2+", "2+", "2+", "2+", "1+");
        var note = Assert.Single(NotesFor(iso, "EL-S5"));
        Assert.Contains("panreactive (5 of 5", note);
        Assert.Contains("autoantibody", note);
        Assert.Contains("adsorption", note);
    }

    [Fact]
    public void NonreactiveEluate_WithPositiveDat_ListsCauses_AndC3OnlyIsExpected()
    {
        using var iso = Setup("EL-S6", "EL-E6", "0", "0", "0");
        iso.Db.UpdateSpecimenDatSplit("EL-S6", "Negative", "2+");
        var note = Assert.Single(NotesFor(iso, "EL-S6"));
        Assert.Contains("is nonreactive (0 of 3", note);
        Assert.Contains("drug-related", note);
        Assert.Contains("Only C3 is reactive", note);
    }

    [Fact]
    public void ReactiveEluateWithoutAnalysis_AsksToAnalyze_AndCandidatesUnchanged()
    {
        using var iso = Setup("EL-S7", "EL-E7", "2+", "0", "0");
        var with = iso.Analyzer.AnalyzeSpecimen("EL-S7", updateDb: false);
        Assert.Contains(with.SpecialReactionNotes, n => n.Contains("Run analysis on the eluate"));

        iso.Db.SetEluateSource("EL-E7", null);
        var without = iso.Analyzer.AnalyzeSpecimen("EL-S7", updateDb: false);
        Assert.Equal(without.Suspected.Keys.OrderBy(k => k), with.Suspected.Keys.OrderBy(k => k));
        Assert.Equal(without.RuledOut.Keys.OrderBy(k => k), with.RuledOut.Keys.OrderBy(k => k));
    }
}
