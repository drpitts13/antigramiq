namespace HemoIQ.Models
{
    public class Rule
    {
        public int RuleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Antibody { get; set; } = string.Empty;
        public string? ExceptionAntigen { get; set; }
        public bool HeterozygousOk { get; set; }
        public int MinRuleoutCount { get; set; } = 3;
        /// <summary>When true, this antibody need not have isolated reactive cells when called with others.</summary>
        public bool SeparationExempt { get; set; }
        /// <summary>Comma-separated partner antigens the exemption covers; blank covers every partner.</summary>
        public string? SeparationExemptFrom { get; set; }

        public string SeparationExemptDisplay =>
            !SeparationExempt ? "No (separation required)" :
            string.IsNullOrWhiteSpace(SeparationExemptFrom) ? "All partners" : SeparationExemptFrom!;
    }
}
