namespace AntibodyPanels.Models
{
    /// <summary>
    /// Permanent patient information keyed by MRN, shared by every specimen
    /// with that MRN. Separate from specimen (current investigation) fields.
    /// </summary>
    public class Patient
    {
        public string Mrn { get; set; } = string.Empty;
        public string? Name { get; set; }
        /// <summary>yyyy-MM-dd.</summary>
        public string? DateOfBirth { get; set; }
        /// <summary>Permanent blood bank comments (e.g. "warm auto since 2024").</summary>
        public string? BloodBankComments { get; set; }
        /// <summary>Special transfusion requirements, e.g. "K-negative, E-negative; irradiated".</summary>
        public string? TransfusionRequirements { get; set; }
        public string? UpdatedAt { get; set; }

        public string Display =>
            string.IsNullOrWhiteSpace(Name) ? Mrn : $"{Name} ({Mrn}){(string.IsNullOrWhiteSpace(DateOfBirth) ? "" : $", DOB {DateOfBirth}")}";
    }
}
