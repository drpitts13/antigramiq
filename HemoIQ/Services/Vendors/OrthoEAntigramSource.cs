namespace HemoIQ.Services.Vendors
{
    /// <summary>
    /// Ortho / QuidelOrtho. The public Quick Antigram page publishes barcode and QR-code
    /// sheets for Ortho's Quick Antigram software, not readable antigen tables, so lots
    /// cannot be downloaded and parsed. Import an e-Antigram PDF or CSV file instead.
    /// </summary>
    public sealed class OrthoEAntigramSource : VendorPanelSourceBase
    {
        public OrthoEAntigramSource(VendorHttpClient http) : base(http) { }

        public override string VendorId => VendorIds.Ortho;
        public override string DisplayName => "Ortho / QuidelOrtho";
        public override bool CanListLots => false;
        public override string? ListLotsUnavailableReason =>
            "QuidelOrtho's public Quick Antigram page publishes antigen data only as barcode and QR-code sheets " +
            "for scanning into Ortho's Quick Antigram software. The antigen results are inside the barcode images, " +
            "not in readable text, so HemoIQ cannot download them. Import the e-Antigram PDF or CSV from your " +
            "QuidelOrtho technical documents with Import file.";
    }
}
