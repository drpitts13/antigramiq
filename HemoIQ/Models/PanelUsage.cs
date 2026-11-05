using System.Collections.Generic;

namespace HemoIQ.Models
{
    /// <summary>How a panel has been used for testing. See <c>DatabaseService.GetPanelUsage</c>.</summary>
    public sealed class PanelUsage
    {
        public List<string> LinkedSpecimens { get; } = new();
        public List<string> ConfirmedSpecimens { get; } = new();
        public int ReactionCount { get; set; }

        /// <summary>True when deleting or replacing the panel would erase test history.</summary>
        public bool ProtectsHistory => ReactionCount > 0 || ConfirmedSpecimens.Count > 0;

        public string Describe()
        {
            var parts = new List<string>();
            if (ReactionCount > 0) parts.Add($"{ReactionCount} recorded reaction row(s)");
            if (ConfirmedSpecimens.Count > 0)
                parts.Add($"confirmed identification on {string.Join(", ", ConfirmedSpecimens)}");
            else if (LinkedSpecimens.Count > 0)
                parts.Add($"linked to {string.Join(", ", LinkedSpecimens)}");
            return parts.Count == 0 ? "not used" : string.Join("; ", parts);
        }
    }
}
