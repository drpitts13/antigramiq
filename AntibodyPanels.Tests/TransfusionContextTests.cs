using AntibodyPanels.Models;
using AntibodyPanels.Services;
using AntibodyPanels.Tests.Infrastructure;

namespace AntibodyPanels.Tests;

[Collection("PersistentDatabase")]
public class TransfusionContextTests
{
    // ── Assessment rules ───────────────────────────────────────────────

    [Fact]
    public void Transfused_WithinWindow_IsUnreliable_AndNamesDaysAndWindow()
    {
        var a = TransfusionContext.Assess("transfused", "2026-10-20", "2026-11-03", 90, "E+", null);
        Assert.True(a.PhenotypeUnreliable);
        Assert.Equal("structured", a.Source);
        Assert.Contains("2026-10-20", a.Explanation);
        Assert.Contains("14 day(s)", a.Explanation);
        Assert.Contains("90-day window", a.Explanation);
    }

    [Fact]
    public void Transfused_OnWindowBoundary_IsStillUnreliable()
    {
        var a = TransfusionContext.Assess("transfused", "2026-08-05", "2026-11-03", 90, null, null);
        Assert.True(a.PhenotypeUnreliable);
    }

    [Fact]
    public void Transfused_OutsideWindow_IsUsable_AndSaysWindowExceeded()
    {
        var a = TransfusionContext.Assess("transfused", "2026-01-01", "2026-11-03", 90, "E+", null);
        Assert.False(a.PhenotypeUnreliable);
        Assert.Contains("outside the configured 90-day window", a.Explanation);
    }

    [Fact]
    public void Transfused_WindowIsConfigurable()
    {
        var shortWindow = TransfusionContext.Assess("transfused", "2026-09-01", "2026-11-03", 30, null, null);
        var longWindow = TransfusionContext.Assess("transfused", "2026-09-01", "2026-11-03", 120, null, null);
        Assert.False(shortWindow.PhenotypeUnreliable);
        Assert.True(longWindow.PhenotypeUnreliable);
    }

    [Fact]
    public void Transfused_WithoutDate_IsUnreliable()
    {
        var a = TransfusionContext.Assess("transfused", null, "2026-11-03", 90, null, null);
        Assert.True(a.PhenotypeUnreliable);
        Assert.Contains("no valid last-transfusion date", a.Explanation);
    }

    [Fact]
    public void Transfused_DateAfterSpecimen_IsUnreliable_ForReview()
    {
        var a = TransfusionContext.Assess("transfused", "2026-11-05", "2026-11-03", 90, null, null);
        Assert.True(a.PhenotypeUnreliable);
        Assert.Contains("after the specimen date", a.Explanation);
    }

    [Fact]
    public void Unknown_UnrecordedNoteWording_IsMissedByKeywords_ButStructuredStatusCatchesIt()
    {
        // "PRBC" is not a keyword. Without a structured status the phenotype is used.
        var unknown = TransfusionContext.Assess("unknown", null, "2026-11-03", 90, "E+", "received 2 PRBC 10/30");
        Assert.False(unknown.PhenotypeUnreliable);
        Assert.Contains("Unknown", unknown.Explanation);

        var structured = TransfusionContext.Assess("transfused", "2026-10-30", "2026-11-03", 90, "E+", "received 2 PRBC 10/30");
        Assert.True(structured.PhenotypeUnreliable);
    }

    [Fact]
    public void Unknown_WithNoteKeyword_FallsBackToNotes_AndSaysSo()
    {
        var a = TransfusionContext.Assess(null, null, "2026-11-03", 90, null, "Recently transfused 2 RBC units");
        Assert.True(a.PhenotypeUnreliable);
        Assert.Equal("notes", a.Source);
        Assert.Contains("Record the transfusion status", a.Explanation);
    }

    [Fact]
    public void NotTransfused_OverridesNegatedNoteKeyword_ButNamesTheTextForReview()
    {
        var a = TransfusionContext.Assess("none", null, "2026-11-03", 90, "E+", "No transfusion history");
        Assert.False(a.PhenotypeUnreliable);
        Assert.Equal("structured", a.Source);
        Assert.Contains("review that text", a.Explanation);
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("TRANSFUSED", "transfused")]
    [InlineData(" none ", "none")]
    [InlineData("bogus", "unknown")]
    public void Status_Normalizes(string? input, string expected) =>
        Assert.Equal(expected, TransfusionStatus.Normalize(input));

    [Fact]
    public void Parser_UsesStructuredAssessment_InsteadOfNoteKeywords()
    {
        var usable = TransfusionContext.Assess("none", null, "2026-11-03", 90, "E+", "transfused in 2019");
        var typing = PatientTypingParser.Parse("E+", null, "transfused in 2019", null, usable);
        Assert.False(typing.PhenotypeUnreliable);
        Assert.Contains(PatientTypingParser.Evaluate(typing),
            c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Against);
    }

    // ── Persistence, audit, and lock ───────────────────────────────────

    [Fact]
    public void NewSpecimen_DefaultsToUnknown()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-DEF", "serum", null);
        var s = iso.Db.GetSpecimen("TX-DEF")!;
        Assert.Equal(TransfusionStatus.Unknown, s.TransfusionStatus);
        Assert.Null(s.LastTransfusionDate);
        Assert.Equal("Unknown", s.TransfusionDisplay);
    }

    [Fact]
    public void UpdateTransfusion_PersistsAndAuditsBeforeAndAfter()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-UPD", "serum", null);
        iso.Db.UpdateSpecimenTransfusion("TX-UPD", "transfused", "2026-10-30");

        var s = iso.Db.GetSpecimen("TX-UPD")!;
        Assert.Equal("transfused", s.TransfusionStatus);
        Assert.Equal("2026-10-30", s.LastTransfusionDate);
        Assert.Equal("Transfused (last 2026-10-30)", s.TransfusionDisplay);

        var ev = Assert.Single(iso.Db.GetAuditEvents(), e => e.Action == "update_transfusion");
        Assert.Equal("TX-UPD", ev.EntityId);
        Assert.Contains("unknown", ev.BeforeJson);
        Assert.Contains("2026-10-30", ev.AfterJson);
    }

    [Fact]
    public void UpdateTransfusion_Unchanged_WritesNoAudit()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-SAME", "serum", null);
        iso.Db.UpdateSpecimenTransfusion("TX-SAME", "unknown", null);
        Assert.DoesNotContain(iso.Db.GetAuditEvents(), e => e.Action == "update_transfusion");
    }

    [Fact]
    public void UpdateTransfusion_NotTransfused_DropsDate()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-NONE", "serum", null);
        iso.Db.UpdateSpecimenTransfusion("TX-NONE", "none", "2026-10-30");
        Assert.Null(iso.Db.GetSpecimen("TX-NONE")!.LastTransfusionDate);
    }

    [Fact]
    public void UpdateTransfusion_RejectsFutureAndInvalidDates()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-BAD", "serum", null);
        var future = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");
        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenTransfusion("TX-BAD", "transfused", future));
        Assert.Throws<ArgumentException>(() => iso.Db.UpdateSpecimenTransfusion("TX-BAD", "transfused", "not a date"));
        Assert.Equal(TransfusionStatus.Unknown, iso.Db.GetSpecimen("TX-BAD")!.TransfusionStatus);
    }

    [Fact]
    public void UpdateTransfusion_BlockedAfterFinalCall()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-LOCK", "serum", null);
        iso.Db.SetSpecimenFinalCall("TX-LOCK", "anti-E", null, "DP");
        Assert.Throws<RecordLockedException>(() =>
            iso.Db.UpdateSpecimenTransfusion("TX-LOCK", "transfused", "2026-10-30"));
    }

    // ── End to end through the analyzer ────────────────────────────────

    private static string SetUpAntiEPattern(IsolatedDatabase iso, string acc, string? notes, string createdDate)
    {
        iso.Db.AddSpecimen(acc, "serum", null, notes: notes, phenotype: "E+", createdDate: createdDate);
        var panelId = iso.Db.AddPanel("P", "L-" + acc, "V", 2, null, false);
        iso.Db.LinkSpecimenPanel(acc, panelId);
        iso.Db.SaveReaction(acc, panelId, "1", "0", "0", "0", "2+");
        iso.Db.SaveReaction(acc, panelId, "2", "0", "0", "0", "2+");
        return acc;
    }

    private static T WithWindow<T>(int days, Func<T> body)
    {
        var previous = AppSettings.Current.RecentTransfusionWindowDays;
        try
        {
            AppSettings.Current.RecentTransfusionWindowDays = days;
            return body();
        }
        finally
        {
            AppSettings.Current.RecentTransfusionWindowDays = previous;
        }
    }

    [Fact]
    public void Analyzer_StructuredRecentTransfusion_MakesPhenotypeUninterpretable_EvenWithoutNoteKeyword()
    {
        using var iso = new IsolatedDatabase();
        SetUpAntiEPattern(iso, "TX-AN1", "received 2 PRBC", "2026-11-03");
        iso.Db.UpdateSpecimenTransfusion("TX-AN1", "transfused", "2026-10-30");

        var result = WithWindow(90, () => iso.Analyzer.AnalyzeSpecimen("TX-AN1", updateDb: false));
        Assert.True(result.PatientPhenotypeUnreliable);
        Assert.Contains("4 day(s)", result.TransfusionContextNote);
        Assert.Contains(result.PatientTypingConsiderations,
            c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Uninterpretable && c.Explanation.Contains("2026-10-30"));
        Assert.DoesNotContain(result.PatientTypingConsiderations,
            c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Against);
        Assert.Contains(result.Suggestions, s => s.Contains("cannot be interpreted") && s.Contains("2026-10-30"));
    }

    [Fact]
    public void Analyzer_TransfusionOutsideWindow_UsesPhenotype_AndExplains()
    {
        using var iso = new IsolatedDatabase();
        SetUpAntiEPattern(iso, "TX-AN2", null, "2026-11-03");
        iso.Db.UpdateSpecimenTransfusion("TX-AN2", "transfused", "2026-01-15");

        var result = WithWindow(90, () => iso.Analyzer.AnalyzeSpecimen("TX-AN2", updateDb: false));
        Assert.False(result.PatientPhenotypeUnreliable);
        Assert.Contains("outside the configured 90-day window", result.TransfusionContextNote);
        Assert.Contains(result.PatientTypingConsiderations,
            c => c.Antibody == "anti-E" && c.Kind == PatientTypingKind.Against);
    }

    [Fact]
    public void Analyzer_NotTransfused_IgnoresNegatedNote()
    {
        using var iso = new IsolatedDatabase();
        SetUpAntiEPattern(iso, "TX-AN3", "No transfusion history", "2026-11-03");
        iso.Db.UpdateSpecimenTransfusion("TX-AN3", "none", null);

        var result = WithWindow(90, () => iso.Analyzer.AnalyzeSpecimen("TX-AN3", updateDb: false));
        Assert.False(result.PatientPhenotypeUnreliable);
        Assert.Contains("review that text", result.TransfusionContextNote);
    }

    // ── Mixed-field autocontrol ────────────────────────────────────────

    private static List<Reaction> AutocontrolRows(string ahg, string? extraPhase = null, string? extraValue = null)
    {
        var ac = new Reaction { CellNumber = "AC", IS = "0", C37 = "0", AHG = ahg };
        if (extraPhase != null) ac.ExtraPhases[extraPhase] = extraValue!;
        return new List<Reaction>
        {
            new() { CellNumber = "1", AHG = "MF" }, // panel-cell MF is not a donor-cell signal
            ac,
        };
    }

    [Fact]
    public void MixedFieldAutocontrol_RecentTransfusion_SuggestsDatAndEluate()
    {
        var t = TransfusionContext.Assess("transfused", "2026-10-25", "2026-11-03", 90, null, null);
        var note = Assert.Single(TransfusionContext.DescribeMixedFieldAutocontrol(AutocontrolRows("MF"), t));
        Assert.Contains("Autocontrol AHG is mixed-field", note);
        Assert.Contains("surviving donor cells", note);
        Assert.Contains("eluate", note);
        Assert.Contains("9 day(s)", note);
        Assert.Contains("not a diagnosis", note);
    }

    [Fact]
    public void MixedFieldAutocontrol_NotTransfused_AsksForHistoryAndTransplantReview()
    {
        var t = TransfusionContext.Assess("none", null, "2026-11-03", 90, null, null);
        var note = Assert.Single(TransfusionContext.DescribeMixedFieldAutocontrol(AutocontrolRows("MF"), t));
        Assert.Contains("Not transfused", note);
        Assert.Contains("transplant", note);
        Assert.DoesNotContain("surviving donor cells", note);
    }

    [Fact]
    public void MixedFieldAutocontrol_OldTransfusion_SaysDonorCellsLessLikely()
    {
        var t = TransfusionContext.Assess("transfused", "2025-01-01", "2026-11-03", 90, null, null);
        var note = Assert.Single(TransfusionContext.DescribeMixedFieldAutocontrol(AutocontrolRows("MF"), t));
        Assert.Contains("outside the configured 90-day window", note);
        Assert.Contains("less likely", note);
    }

    [Fact]
    public void MixedFieldAutocontrol_Unknown_AsksToRecordStatus_AndNamesExtraPhase()
    {
        var t = TransfusionContext.Assess("unknown", null, "2026-11-03", 90, null, null);
        var note = Assert.Single(TransfusionContext.DescribeMixedFieldAutocontrol(
            AutocontrolRows("MF", "Gel", "mf"), t));
        Assert.Contains("Autocontrol AHG, Gel is mixed-field", note);
        Assert.Contains("record the transfusion status", note);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2+")]
    [InlineData("NT")]
    public void NonMixedFieldAutocontrol_GivesNoNote(string ahg)
    {
        var t = TransfusionContext.Assess("transfused", "2026-10-25", "2026-11-03", 90, null, null);
        Assert.Empty(TransfusionContext.DescribeMixedFieldAutocontrol(AutocontrolRows(ahg), t));
    }

    [Fact]
    public void Analyzer_MixedFieldAutocontrol_AddsNote_WithoutChangingCandidates()
    {
        using var iso = new IsolatedDatabase();
        SetUpAntiEPattern(iso, "TX-MF", null, "2026-11-03");
        var panelId = iso.Db.GetSpecimenPanels("TX-MF").Single().PanelId;
        iso.Db.UpdateSpecimenTransfusion("TX-MF", "transfused", "2026-10-28");
        var before = WithWindow(90, () => iso.Analyzer.AnalyzeSpecimen("TX-MF", updateDb: false));

        iso.Db.SaveReaction("TX-MF", panelId, "AC", "0", "0", "MF", "NT");
        var after = WithWindow(90, () => iso.Analyzer.AnalyzeSpecimen("TX-MF", updateDb: false));

        Assert.Contains(after.SpecialReactionNotes, n => n.Contains("Autocontrol AHG is mixed-field") && n.Contains("eluate"));
        Assert.Contains(after.Suggestions, s => s.Contains("Autocontrol AHG is mixed-field"));
        Assert.Equal(before.Suspected.Keys.OrderBy(k => k), after.Suspected.Keys.OrderBy(k => k));
        Assert.Equal(before.RuledOut.Keys.OrderBy(k => k), after.RuledOut.Keys.OrderBy(k => k));
    }

    [Fact]
    public void WorksheetReport_ShowsTransfusionLine()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("TX-RPT", "serum", null);
        iso.Db.UpdateSpecimenTransfusion("TX-RPT", "transfused", "2026-10-30");
        var reports = new ReportService(iso.Db);
        Assert.Contains("Transfused (last 2026-10-30)",
            reports.GeneratePreviewText(ReportType.SpecimenSummary, "TX-RPT"));
        Assert.Contains("Transfusion: Transfused (last 2026-10-30)",
            reports.GeneratePreviewText(ReportType.ClinicalIdentification, "TX-RPT"));
    }
}
