using HemoIQ.Data;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

/// <summary>Guards against tests writing to the user's real HemoIQ data.</summary>
public class TestIsolationTests
{
    private static readonly string RealRoot = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataPaths.FolderName);

    [Fact]
    public void DataRoot_DuringTests_IsATemporaryFolder_NotUserAppData()
    {
        Assert.Equal(TestDataRoot.Path, AppDataPaths.Root);
        Assert.False(AppDataPaths.Root.StartsWith(RealRoot, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LogsAndSettings_ResolveUnderTheTestRoot()
    {
        Assert.StartsWith(TestDataRoot.Path, AppLog.LogDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(TestDataRoot.Path, SettingsService.DefaultPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultDatabase_OpensUnderTheTestRoot()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HEMOIQ_DB")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTIBODY_PANELS_DB")))
            return; // an explicit override is the caller's choice
        using var db = new DatabaseService();
        Assert.StartsWith(TestDataRoot.Path, db.DbPath, StringComparison.OrdinalIgnoreCase);
    }
}
