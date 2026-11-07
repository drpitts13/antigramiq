using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Suggested criterion for calling two or more antibodies together: each called
    /// antibody needs a configured number of reactive cells that carry its antigen and
    /// are typed negative for every other called antigen (for anti-e + anti-K, reactive
    /// e+ K- cells and reactive e- K+ cells). Patient history exempts that antibody;
    /// a rule on the Rules tab can exempt it from some or all partners. Decision support,
    /// not a block.
    /// </summary>
    public static class MultipleAntibodySeparation
    {
        public const int MinCount = 0;
        public const int MaxCount = 5;

        public static int Clamp(int count) => Math.Clamp(count, MinCount, MaxCount);

        /// <summary>Strips "anti-" and returns the antigen with its case, or null when unknown.</summary>
        public static string? AntigenOf(string antibody)
        {
            var t = (antibody ?? string.Empty).Trim();
            if (t.StartsWith("anti-", StringComparison.OrdinalIgnoreCase)) t = t[5..].Trim();
            return AntigenConstants.IsKnown(t) ? t : null;
        }

        /// <summary>Partners a rule exempts for this antibody. Null: no rule exemption. Empty set: all partners.</summary>
        public static HashSet<string>? RuleExemptPartners(string antibody, IEnumerable<Rule> rules)
        {
            var rule = RuleoutPolicy.FindAntibodyRule(antibody, rules);
            if (rule == null || !rule.SeparationExempt) return null;
            return ParseAntigenList(rule.SeparationExemptFrom);
        }

        public static HashSet<string> ParseAntigenList(string? text) =>
            new((text ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Select(a => a.StartsWith("anti-", StringComparison.OrdinalIgnoreCase) ? a[5..] : a)
                .Where(a => a.Length > 0), StringComparer.Ordinal);

        public static List<SeparationEvaluation> Evaluate(
            IEnumerable<string> called,
            Dictionary<int, List<Reaction>> byRun,
            Dictionary<int, RunContext> contexts,
            IReadOnlyDictionary<int, List<PanelCell>> cellsByPanel,
            int required,
            IEnumerable<Rule> rules,
            ISet<string> historical,
            Func<RunContext, string, bool>? runExcluded = null)
        {
            var list = new List<SeparationEvaluation>();
            required = Clamp(required);
            var calledAntigens = called
                .Select(ab => (Antibody: ab, Antigen: AntigenOf(ab)))
                .Where(x => x.Antigen != null)
                .GroupBy(x => x.Antigen!, StringComparer.Ordinal)
                .Select(g => (Antibody: "anti-" + g.Key, Antigen: g.Key))
                .ToList();
            if (required == 0 || calledAntigens.Count < 2) return list;
            var ruleList = rules.ToList();

            foreach (var (antibody, antigen) in calledAntigens)
            {
                var partners = calledAntigens.Where(c => c.Antigen != antigen).Select(c => c.Antigen).ToList();
                var exemptFrom = RuleExemptPartners(antibody, ruleList);
                var ruleExempt = exemptFrom == null
                    ? new List<string>()
                    : partners.Where(p => exemptFrom.Count == 0 || exemptFrom.Contains(p)).ToList();
                var tested = partners.Except(ruleExempt).ToList();
                var evaluation = new SeparationEvaluation
                {
                    Antibody = antibody,
                    Antigen = antigen,
                    Partners = partners,
                    RuleExemptPartners = ruleExempt,
                    Required = required,
                };

                if (historical.Contains(antibody))
                    evaluation.ExemptReason = "patient history";
                else if (tested.Count == 0)
                    evaluation.ExemptReason = "rule";
                else
                    evaluation.Cells = IsolatedCells(antigen, tested, byRun, contexts, cellsByPanel, antibody, runExcluded);

                evaluation.IsolatedCount = evaluation.Cells.Count;
                evaluation.Explanation = Explain(evaluation, tested);
                list.Add(evaluation);
            }
            return list;
        }

        private static List<string> IsolatedCells(
            string antigen, List<string> partners,
            Dictionary<int, List<Reaction>> byRun,
            Dictionary<int, RunContext> contexts,
            IReadOnlyDictionary<int, List<PanelCell>> cellsByPanel,
            string antibody,
            Func<RunContext, string, bool>? runExcluded)
        {
            var seen = new HashSet<(int, string)>();
            var cells = new List<string>();
            foreach (var (runId, runReactions) in byRun)
            {
                if (!contexts.TryGetValue(runId, out var ctx)) continue;
                if (!ctx.TypesAntigen(antigen)) continue;
                if (ctx.GetAntigenEffect(antigen) == AntigenEffect.Destroyed) continue;
                if (runExcluded?.Invoke(ctx, antibody) == true) continue;
                if (partners.Any(p => !ctx.TypesAntigen(p))) continue;
                if (!cellsByPanel.TryGetValue(ctx.Run.PanelId, out var panelCells)) continue;
                var cellDict = panelCells.ToDictionary(c => c.CellNumber);

                foreach (var rxn in runReactions)
                {
                    if (rxn.CellNumber == "AC") continue;
                    if (!cellDict.TryGetValue(rxn.CellNumber, out var cell)) continue;
                    if (!ctx.IsPositive(rxn)) continue;
                    if (!ctx.IsAntigenPresent(cell, antigen)) continue;
                    // A partner antigen must be typed and negative; NT leaves the cell unproven.
                    if (partners.Any(p => !cell.HasTypedAntigen(p) || ctx.IsAntigenPresent(cell, p))) continue;
                    if (!seen.Add((ctx.Run.PanelId, rxn.CellNumber))) continue;
                    var label = string.IsNullOrWhiteSpace(ctx.Run.PanelName) ? $"Panel {ctx.Run.PanelId}" : ctx.Run.PanelName;
                    cells.Add($"{label} cell {rxn.CellNumber}");
                }
            }
            return cells;
        }

        private static string Explain(SeparationEvaluation e, List<string> tested)
        {
            var name = RuleoutPolicy.DisplayAntibody(e.Antibody);
            var ruleNote = e.RuleExemptPartners.Count > 0 && e.ExemptReason == null
                ? $" A rule exempts it from {string.Join(", ", e.RuleExemptPartners)}."
                : "";
            if (e.ExemptReason == "patient history")
                return $"{name} is known from patient history, so it does not need its own isolated reactive cells " +
                       "when called with other antibodies.";
            if (e.ExemptReason == "rule")
                return $"{name} is exempt by rule from separation from {string.Join(", ", e.RuleExemptPartners)}.";

            var phenotype = $"{e.Antigen}+ {string.Join(" ", tested.Select(p => p + "-"))}";
            var cellsText = e.Cells.Count > 0 ? $" ({string.Join(", ", e.Cells)})" : "";
            if (e.MeetsCriterion)
                return $"{name} has {e.IsolatedCount} reactive {phenotype} cell(s){cellsText}, meeting the suggested " +
                       $"multiple-antibody criterion of {e.Required}.{ruleNote}";
            var need = e.Required - e.IsolatedCount;
            return $"{name} has {e.IsolatedCount} of {e.Required} reactive {phenotype} cell(s) needed to call it " +
                   $"alongside {string.Join(", ", tested.Select(p => "anti-" + p))}{cellsText}. Test {need} more " +
                   $"{phenotype} cell(s), or document why the combination is accepted. Suggested criterion, not a rule-out.{ruleNote}";
        }
    }
}
