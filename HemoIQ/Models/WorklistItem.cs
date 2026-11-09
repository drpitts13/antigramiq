namespace HemoIQ.Models
{
    public enum WorklistKind
    {
        IncompleteReactions,
        StaleAnalysis,
        ExpiringSpecimen,
        ExpiringPanel,
        ExpiredSpecimen,
        ExpiredPanel,
        AwaitingSupervisorReview,
        ExtraAntigenReview,
        ExpiredReagentUse,
        UntypedPanel,
        DuplicateLot,
        InconsistentStatus
    }

    public class WorklistItem
    {
        public WorklistKind Kind { get; set; }
        public string KindLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string UrgencyLabel { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? AccessionNumber { get; set; }
        public int? PanelId { get; set; }
        /// <summary>The antigen an <see cref="WorklistKind.ExtraAntigenReview"/> item is about.</summary>
        public string? Antigen { get; set; }
        public string TargetTab { get; set; } = "Specimens";

        public bool MatchesFilter(string? query) =>
            TextFilter.Matches(query, Title, Detail, KindLabel, UrgencyLabel, AccessionNumber);
    }
}
