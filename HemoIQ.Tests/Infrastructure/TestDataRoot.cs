using System.Runtime.CompilerServices;
using HemoIQ.Services;

namespace HemoIQ.Tests.Infrastructure;

/// <summary>
/// Points the application's data root at a per-run temporary folder before any
/// test runs, so the default database, settings, logs, and import artifacts never
/// resolve to the user's real %AppData%\HemoIQ.
/// </summary>
internal static class TestDataRoot
{
    public static string Path { get; private set; } = string.Empty;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hemoiq_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
        AppDataPaths.RootOverride = Path;
    }
}
