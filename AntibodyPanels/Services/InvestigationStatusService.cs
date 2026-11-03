using System.Collections.Generic;
using System.Linq;
using AntibodyPanels.Data;
using AntibodyPanels.Models;

namespace AntibodyPanels.Services
{
    public sealed class InvestigationStep
    {
        public string Name { get; init; } = string.Empty;
        public bool Done { get; init; }
        public string Display => (Done ? "✓ " : "○ ") + Name;
    }

    public sealed class InvestigationStatus
    {
        public string Label { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public List<InvestigationStep> Steps { get; init; } = new();

        public string StepsDisplay => string.Join("   ", Steps.Select(s => s.Display));
        public string Summary => string.IsNullOrEmpty(NextAction) ? Label : $"{Label} — Next: {NextAction}";
    }

    /// <summary>
    /// Derives an investigation's status from recorded work (panels,
    /// reactions, analysis time, final call, audit). Nothing is stored, so the
    /// status cannot drift from the underlying records.
    /// </summary>
    public static class InvestigationStatusService
    {
        public const string AwaitingPanel = "Awaiting panel";
        public const string TestingInProgress = "Testing in progress";
        public const string AnalysisNeeded = "Analysis needed";
        public const string AwaitingIdentification = "Awaiting identification";
        public const string AwaitingSupervisorReview = "Awaiting supervisor review";
        public const string Completed = "Completed";
        public const string Amended = "Amended";
        public const string Reopened = "Reopened";
        public const string InactiveIncomplete = "Inactive – not completed";

        public static InvestigationStatus Compute(DatabaseService db, Specimen s) =>
            Compute(db, s, AppSettings.Current.RequireSupervisorReview);

        public static InvestigationStatus Compute(DatabaseService db, Specimen s, bool requireReview)
        {
            var panels = db.GetSpecimenPanels(s.AccessionNumber);
            var incomplete = panels.Count == 0
                ? new List<string>()
                : db.GetIncompleteReactionPanels(s.AccessionNumber);
            var cleared = db.CountAuditEvents("clear_final_call", s.AccessionNumber);

            var hasPanel = panels.Count > 0;
            var reactionsDone = hasPanel && incomplete.Count == 0;
            var analysisCurrent = hasPanel && s.LastAnalyzedAt != null && !s.IsAnalysisStale;

            var steps = new List<InvestigationStep>
            {
                new() { Name = "Received", Done = true },
                new() { Name = "Panel attached", Done = hasPanel },
                new() { Name = "Reactions complete", Done = reactionsDone },
                new() { Name = "Analysis current", Done = analysisCurrent },
                new() { Name = "ID confirmed", Done = s.HasFinalCall },
            };
            if (requireReview)
                steps.Add(new InvestigationStep { Name = "Supervisor reviewed", Done = s.HasFinalCall && s.IsReviewed });

            if (s.HasFinalCall && requireReview && !s.IsReviewed)
                return new InvestigationStatus
                {
                    Label = AwaitingSupervisorReview,
                    NextAction = $"A supervisor other than {s.IdentifiedBy ?? "the confirming technologist"} reviews the identification (Supervisor Review on the Specimens tab).",
                    Steps = steps,
                };

            if (s.HasFinalCall)
                return new InvestigationStatus
                {
                    Label = cleared > 0 ? Amended : Completed,
                    NextAction = cleared > 0
                        ? $"Identification was cleared {cleared} time(s) and confirmed again; see the audit log for reasons."
                        : string.Empty,
                    Steps = steps,
                };

            string label, next;
            if (!hasPanel)
            {
                label = AwaitingPanel;
                next = "Attach a panel on the Specimens tab.";
            }
            else if (!reactionsDone)
            {
                label = TestingInProgress;
                next = "Finish reactions: " + string.Join("; ", incomplete) + ".";
            }
            else if (!analysisCurrent)
            {
                label = AnalysisNeeded;
                next = s.LastAnalyzedAt == null
                    ? "Run analysis."
                    : "Reactions changed since the last analysis; run analysis again.";
            }
            else
            {
                label = AwaitingIdentification;
                next = "Review the analysis and confirm or record the identification.";
            }

            if (cleared > 0)
            {
                next = $"A confirmed identification was cleared; current step: {label.ToLowerInvariant()}. {next}";
                label = Reopened;
            }
            if (!s.IsActive)
            {
                next = $"Specimen is inactive without a confirmed identification ({label.ToLowerInvariant()}). Reactivate it to continue, or leave it closed.";
                label = InactiveIncomplete;
            }
            return new InvestigationStatus { Label = label, NextAction = next, Steps = steps };
        }
    }
}
