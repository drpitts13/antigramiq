using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AntibodyPanels.Models;

namespace AntibodyPanels.Services
{
    /// <summary>
    /// Compares known antibodies with the patient's recorded special
    /// transfusion requirements and names antigen-negative requirements that
    /// are not written down. A review prompt, not an order.
    /// </summary>
    public static class TransfusionRequirementCheck
    {
        public static List<string> MissingAntigenNegative(
            IEnumerable<string> knownAntibodies, Patient? patient, string? mrn)
        {
            var notes = new List<string>();
            if (string.IsNullOrWhiteSpace(mrn)) return notes;
            var requirements = patient?.TransfusionRequirements ?? string.Empty;

            foreach (var ab in knownAntibodies.Distinct(StringComparer.Ordinal))
            {
                if (!ab.StartsWith("anti-", StringComparison.OrdinalIgnoreCase)) continue;
                var antigen = ab[5..].Trim();
                if (!AntigenConstants.IsKnown(antigen)) continue;
                if (MentionsAntigenNegative(requirements, antigen)) continue;

                notes.Add(patient == null
                    ? $"{ab} is known for MRN {mrn}, but no patient record exists to hold special transfusion requirements. " +
                      $"Review whether {antigen}-negative units are required and record it on the patient. Not an order."
                    : $"{ab} is known for MRN {mrn}, but the patient's special transfusion requirements do not mention {antigen}-negative units. " +
                      "Review and update the patient record. Not an order.");
            }
            return notes;
        }

        /// <summary>
        /// True when the text names the antigen followed by "-", "−", "neg", or
        /// "negative". Case-sensitive so C and c, E and e stay distinct.
        /// </summary>
        public static bool MentionsAntigenNegative(string? text, string antigen)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var pattern = $@"(?<![A-Za-z]){Regex.Escape(antigen)}(?![A-Za-z0-9])\s*(?:-|−|–|\(\s*-\s*\)|(?i:neg))";
            if (Regex.IsMatch(text, pattern)) return true;
            // Paired notation: Fy(a-), Jk(b−).
            var pair = Regex.Match(antigen, "^(Fy|Jk|Kp|Js|Lu|Le)([ab])$");
            return pair.Success && Regex.IsMatch(text,
                $@"(?<![A-Za-z]){pair.Groups[1].Value}\s*\(\s*{pair.Groups[2].Value}\s*[-−–]");
        }
    }
}
