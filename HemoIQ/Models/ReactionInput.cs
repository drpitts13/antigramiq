using System.Collections.Generic;

namespace HemoIQ.Models
{
    /// <summary>One cell's grades as entered, for a batch reaction save.</summary>
    public sealed record ReactionInput(string CellNumber, string IS, string C37, string AHG, string CC,
        IReadOnlyDictionary<string, string>? ExtraPhases = null);
}
