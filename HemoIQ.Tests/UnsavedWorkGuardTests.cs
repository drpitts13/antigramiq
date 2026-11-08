using HemoIQ.Services;

namespace HemoIQ.Tests;

public class UnsavedWorkGuardTests
{
    [Fact]
    public void QuitWarning_NullWhenNothingUnsaved_ListsEachUnsavedItem()
    {
        Assert.Null(UnsavedWorkGuard.QuitWarning(null, null));
        Assert.Null(UnsavedWorkGuard.QuitWarning("  ", ""));

        var grades = UnsavedWorkGuard.QuitWarning("2026-002 (Panel A, Untreated)", null)!;
        Assert.Contains("• Reaction grades for 2026-002 (Panel A, Untreated) are not saved.", grades);
        Assert.DoesNotContain("Antigen typing", grades);
        Assert.EndsWith("Quit without saving?", grades);

        var both = UnsavedWorkGuard.QuitWarning("2026-002 (Panel A, Untreated)", "Lot 9")!;
        Assert.Contains("• Antigen typing on panel Lot 9 is still being edited", both);
    }

    [Fact]
    public void AnalysisPrompt_OnlyForTheSpecimenWithUnsavedGrades()
    {
        Assert.True(UnsavedWorkGuard.AnalysisWouldIgnoreUnsavedGrades("2026-002", "2026-002"));
        Assert.True(UnsavedWorkGuard.AnalysisWouldIgnoreUnsavedGrades("ab-1", "AB-1"));
        Assert.False(UnsavedWorkGuard.AnalysisWouldIgnoreUnsavedGrades("2026-002", "2026-003"));
        Assert.False(UnsavedWorkGuard.AnalysisWouldIgnoreUnsavedGrades("2026-002", null));
        Assert.False(UnsavedWorkGuard.AnalysisWouldIgnoreUnsavedGrades(null, null));
        Assert.Contains("Analysis uses saved grades only", UnsavedWorkGuard.AnalysisPrompt("2026-002 (P, Untreated)"));
    }
}
