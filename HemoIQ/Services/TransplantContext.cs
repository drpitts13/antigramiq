using System;
using System.Globalization;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>Hematopoietic progenitor cell (stem-cell or marrow) transplant recorded on the patient.</summary>
    public static class HpcTransplant
    {
        public const string None = "none";
        public const string Autologous = "autologous";
        public const string Allogeneic = "allogeneic";

        public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Autologous or "auto" => Autologous,
            Allogeneic or "allo" => Allogeneic,
            _ => None,
        };

        public static string Label(string? value) => Normalize(value) switch
        {
            Autologous => "Autologous HPC transplant",
            Allogeneic => "Allogeneic HPC transplant",
            _ => "None recorded",
        };
    }

    public static class TransplantDisplay
    {
        /// <summary>"Allogeneic HPC transplant, 2026-05-01", or "None recorded".</summary>
        public static string Text(Patient p) =>
            HpcTransplant.Normalize(p.HpcTransplant) == HpcTransplant.None
                ? "None recorded"
                : HpcTransplant.Label(p.HpcTransplant) + (string.IsNullOrWhiteSpace(p.HpcTransplantDate) ? ", date not recorded" : $", {p.HpcTransplantDate}");
    }

    /// <summary>
    /// After an allogeneic HPC transplant the red cells come, in part or entirely, from the donor,
    /// and DNA from blood or marrow is the donor's. A serologic phenotype then does not show the
    /// patient's own antigens, and a genotype may be the donor's unless it was done before the
    /// transplant or on a non-hematopoietic sample. An autologous transplant changes neither.
    /// Applies only to specimens collected on or after the transplant date (or any date when it
    /// is unknown). Evidence wording only; it never adds or removes an antibody.
    /// </summary>
    public static class TransplantContext
    {
        public static bool AffectsSpecimen(Patient? patient, string? specimenDate)
        {
            if (patient == null || HpcTransplant.Normalize(patient.HpcTransplant) != HpcTransplant.Allogeneic) return false;
            if (!TryDate(patient.HpcTransplantDate, out var transplant)) return true;
            return !TryDate(specimenDate, out var specimen) || specimen.Date >= transplant.Date;
        }

        public static string Explanation(Patient patient) =>
            "An allogeneic HPC transplant is recorded" +
            (TryDate(patient.HpcTransplantDate, out var d) ? $" on {d:yyyy-MM-dd}" : " (date not recorded)") +
            ", so red cells may be donor-derived or mixed and serologic typing does not show the patient's own antigens.";

        /// <summary>Folds the transplant into the typing-reliability assessment used by analysis.</summary>
        public static TransfusionAssessment Apply(TransfusionAssessment transfusion, Patient? patient, string? specimenDate)
        {
            if (!AffectsSpecimen(patient, specimenDate)) return transfusion;
            var note = Explanation(patient!);
            return new TransfusionAssessment
            {
                PhenotypeUnreliable = true,
                GenotypeMayBeDonor = true,
                Explanation = transfusion.PhenotypeUnreliable ? $"{transfusion.Explanation} {note}" : note,
                Source = transfusion.Source,
                Status = transfusion.Status,
            };
        }

        private static bool TryDate(string? text, out DateTime date) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
