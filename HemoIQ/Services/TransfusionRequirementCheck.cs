using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Compares known antibodies with the patient's structured antigen-negative
    /// requirement and names antigens that are missing from it. A requirement
    /// written only in the free-text requirements still raises a prompt to add it
    /// to the structured list. A review prompt, not an order.
    /// </summary>
    public static class TransfusionRequirementCheck
    {
        public static List<string> MissingAntigenNegative(
            IEnumerable<string> knownAntibodies, Patient? patient, string? mrn)
        {
            var notes = new List<string>();
            if (string.IsNullOrWhiteSpace(mrn)) return notes;
            var requirements = patient?.TransfusionRequirements ?? string.Empty;
            var structured = AntigenNegativeRequirement.Antigens(patient?.AntigenNegative);

            foreach (var ab in knownAntibodies.Distinct(StringComparer.Ordinal))
            {
                if (!ab.StartsWith("anti-", StringComparison.OrdinalIgnoreCase)) continue;
                var antigen = ab[5..].Trim();
                if (!AntigenConstants.IsKnown(antigen)) continue;
                if (structured.Contains(antigen, StringComparer.Ordinal)) continue;
                if (MentionsAntigenNegative(requirements, antigen))
                {
                    notes.Add($"{ab} is known for MRN {mrn}. The free-text transfusion requirements mention {antigen}-negative units, " +
                              $"but {antigen} is not in the patient's structured Antigen-negative list. Add it so the requirement is explicit. Not an order.");
                    continue;
                }

                notes.Add(patient == null
                    ? $"{ab} is known for MRN {mrn}, but no patient record exists to hold special transfusion requirements. " +
                      $"Review whether {antigen}-negative units are required and record it on the patient. Not an order."
                    : $"{ab} is known for MRN {mrn}, but the patient record does not require {antigen}-negative units. " +
                      $"Review and add {antigen} to Antigen-negative on the patient record. Not an order.");
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
