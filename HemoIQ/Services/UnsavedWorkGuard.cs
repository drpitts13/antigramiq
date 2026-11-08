using System.Collections.Generic;
using System.Linq;

namespace HemoIQ.Services
{
    /// <summary>
    /// Wording and decisions for unsaved work that another action would lose or bypass:
    /// quitting with unsaved reaction grades or an open antigram edit, and running analysis
    /// while the Reactions tab holds unsaved grades for the same specimen.
    /// </summary>
    public static class UnsavedWorkGuard
    {
        /// <summary>Null when nothing is unsaved; otherwise the quit warning listing each item.</summary>
        public static string? QuitWarning(string? unsavedGrades, string? panelTypingEdit)
        {
            var items = new List<string>();
            if (!string.IsNullOrWhiteSpace(unsavedGrades))
                items.Add($"Reaction grades for {unsavedGrades} are not saved.");
            if (!string.IsNullOrWhiteSpace(panelTypingEdit))
                items.Add($"Antigen typing on panel {panelTypingEdit} is still being edited and may not be saved.");
            if (items.Count == 0) return null;
            return "Quitting now will lose this work:\n\n" +
                   string.Join("\n", items.Select(i => "• " + i)) +
                   "\n\nQuit without saving?";
        }

        /// <summary>True when analysis of <paramref name="analysisAccession"/> would ignore unsaved grades.</summary>
        public static bool AnalysisWouldIgnoreUnsavedGrades(string? analysisAccession, string? unsavedAccession) =>
            !string.IsNullOrEmpty(analysisAccession) &&
            string.Equals(analysisAccession, unsavedAccession, System.StringComparison.OrdinalIgnoreCase);

        public static string AnalysisPrompt(string unsavedGrades) =>
            $"Reaction grades for {unsavedGrades} are entered on the Reactions tab but not saved. " +
            "Analysis uses saved grades only.\n\n" +
            "Yes — save those grades, then analyze.\n" +
            "No — analyze the saved grades without them.\n" +
            "Cancel — do nothing.";
    }
}
