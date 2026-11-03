using HemoIQ.Data;
using HemoIQ.Services.Vendors;
using HemoIQ.Tests.Infrastructure;
using Xunit.Abstractions;

namespace HemoIQ.Tests;

public class VendorLiveDownloadTests
{
    private readonly ITestOutputHelper _output;

    public VendorLiveDownloadTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task LiveCatalog_DownloadsAndPersistsSupportedVendors()
    {
        using var iso = new IsolatedDatabase();
        var results = await VendorLiveImport.ImportRandomLotPerVendorAsync(iso.Db);
        foreach (var row in results)
            _output.WriteLine($"{row.Vendor}: supported={row.DownloadSupported} persisted={row.Persisted} lot={row.LotNumber} {row.Message}");

        Assert.Contains(results, r => r.Vendor == VendorIds.Immucor && !r.DownloadSupported);
        Assert.Contains(results, r => r.Vendor == VendorIds.Grifols && !r.DownloadSupported);
        Assert.Contains(results, r => r.Vendor == VendorIds.Medion && !r.DownloadSupported);

        var persisted = results.Where(r => r.Persisted).ToList();
        Assert.NotEmpty(persisted);
        Assert.Contains(persisted, r => r.Vendor == VendorIds.BioRad);

        foreach (var row in persisted)
        {
            var panel = iso.Db.GetPanel(row.PanelId!.Value);
            Assert.NotNull(panel);
            var cells = iso.Db.GetPanelCells(panel!.PanelId);
            Assert.True(cells.Count >= 8, $"{row.Vendor} imported fewer than 8 cells");
            Assert.Contains(cells, c => c.GetAntigen("D") == "+" || c.GetAntigen("D") == "-");
            Assert.False(string.IsNullOrWhiteSpace(panel.ImportedAt));
            _output.WriteLine($"{row.Vendor} {panel.LotNumber} display antigens: {string.Join(",", iso.Db.GetPanelDisplayAntigens(panel.PanelId))}");
        }
        // Tests persist only to the isolated database. Real lots reach a user's database
        // through Panels › Download from vendor, which shows the import review first.
    }

    [Fact]
    public async Task PublicPdf_ImportFile_ParsesKnownVendorAntigrams()
    {
        using var http = new VendorHttpClient();
        using var catalog = new VendorCatalogService();
        using var iso = new IsolatedDatabase();
        var importer = new VendorPanelImportService(iso.Db);
        var dir = Path.Combine(Path.GetTempPath(), "HemoIQVendorImport");
        Directory.CreateDirectory(dir);

        var files = new (string Vendor, string Url, string FileName)[]
        {
            (VendorIds.BioRad,
                "https://backend.ih-area.bio-rad.com/system/files/F06171.31X_V.01.pdf",
                "F06171.31X_V.01.pdf"),
            (VendorIds.Quotient,
                "https://alivedx.com/wp-content/uploads/2023/10/V265925.pdf",
                "V265925.pdf"),
            (VendorIds.Quotient,
                "https://alivedx.com/wp-content/uploads/2023/10/V265842_V265844.pdf",
                "V265842_V265844.pdf"),
        };

        foreach (var (vendor, url, fileName) in files)
        {
            var path = Path.Combine(dir, fileName);
            var bytes = await http.GetBytesAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            Assert.True(bytes.Length > 1000, $"{fileName} download was too small ({bytes.Length} bytes)");

            var parsed = catalog.ImportFile(vendor, path);
            parsed.SourceUrl ??= url;
            parsed.SourceFormat = "pdf";
            _output.WriteLine(
                $"{vendor} {fileName}: success={parsed.Success} cells={parsed.Cells.Count} " +
                $"lot={parsed.LotNumber} errors={string.Join("; ", parsed.Errors)}");
            Assert.True(parsed.Success, $"{fileName}: " + string.Join("\n", parsed.Errors));
            Assert.False(string.IsNullOrWhiteSpace(parsed.LotNumber), $"{fileName} parsed cells but no lot number");
            Assert.True(parsed.Cells.Count >= 8, $"{fileName} parsed {parsed.Cells.Count} cells");
            Assert.Contains(parsed.Cells, c => c.GetAntigen("D") == "+" || c.GetAntigen("D") == "-");

            var id = importer.Persist(parsed, replaceExisting: true);
            Assert.NotNull(iso.Db.FindPanelByVendorLot(parsed.Vendor, parsed.LotNumber));
            _output.WriteLine($"Persisted {vendor} lot {parsed.LotNumber} as panel #{id} from {path}");
        }
    }
}
