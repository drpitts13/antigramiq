using System;

namespace HemoIQ.Models
{
    public class RecordLockedException : InvalidOperationException
    {
        public string AccessionNumber { get; }

        public bool Cancelled { get; }

        public RecordLockedException(string accessionNumber, bool cancelled = false)
            : base(cancelled
                ? $"Investigation {accessionNumber} is cancelled and locked. Reinstate it with a reason before editing reactions, panels, analysis, or the identification."
                : $"Specimen {accessionNumber} has a confirmed identification and is locked. " +
                  "Clear the confirmation with a reason before editing reactions, panels, or analysis.")
        {
            AccessionNumber = accessionNumber;
            Cancelled = cancelled;
        }
    }
}
