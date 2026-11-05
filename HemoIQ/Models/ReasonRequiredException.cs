using System;
using System.Collections.Generic;

namespace HemoIQ.Models
{
    /// <summary>Thrown when a change to recorded data needs a reason and none was given.</summary>
    public sealed class ReasonRequiredException : InvalidOperationException
    {
        public IReadOnlyList<string> Changes { get; }

        public ReasonRequiredException(string message, IReadOnlyList<string> changes) : base(message) =>
            Changes = changes;
    }
}
