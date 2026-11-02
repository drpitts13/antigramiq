using AntigramIQ.Services;

namespace AntigramIQ.Tests;

public sealed class AppDataMigrationTests : IDisposable
{
    private readonly string _base =
        Path.Combine(Path.GetTempPath(), $"antigramiq_migration_{Guid.NewGuid():N}");

    public AppDataMigrationTests() => Directory.CreateDirectory(_base);

    public void Dispose()
    {
        try { Directory.Delete(_base, recursive: true); } catch { /* best effort cleanup */ }
    }

    private string Legacy => Path.Combine(_base, AppDataPaths.LegacyFolderName);
    private string Current => Path.Combine(_base, AppDataPaths.FolderName);

    [Fact]
    public void LegacyFolderIsMovedAndDatabaseRenamed()
    {
        Directory.CreateDirectory(Path.Combine(Legacy, "logs"));
        File.WriteAllText(Path.Combine(Legacy, "antibody_panels.db"), "db");
        File.WriteAllText(Path.Combine(Legacy, "antibody_panels.db-wal"), "wal");
        File.WriteAllText(Path.Combine(Legacy, "antibody_panels.db-shm"), "shm");
        File.WriteAllText(Path.Combine(Legacy, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(Legacy, "logs", "app.log"), "log");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.NotNull(note);
        Assert.False(Directory.Exists(Legacy));
        Assert.Equal("db", File.ReadAllText(Path.Combine(Current, "antigramiq.db")));
        Assert.Equal("wal", File.ReadAllText(Path.Combine(Current, "antigramiq.db-wal")));
        Assert.Equal("shm", File.ReadAllText(Path.Combine(Current, "antigramiq.db-shm")));
        Assert.True(File.Exists(Path.Combine(Current, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Current, "logs", "app.log")));
        Assert.False(File.Exists(Path.Combine(Current, "antibody_panels.db")));
    }

    [Fact]
    public void LegacyDataMergesIntoCurrentFolderThatHasNoDatabase()
    {
        Directory.CreateDirectory(Path.Combine(Legacy, "logs"));
        Directory.CreateDirectory(Path.Combine(Current, "logs"));
        File.WriteAllText(Path.Combine(Legacy, "antibody_panels.db"), "db");
        File.WriteAllText(Path.Combine(Legacy, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(Legacy, "logs", "app-old.log"), "old log");
        File.WriteAllText(Path.Combine(Current, "logs", "app-new.log"), "new log");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.NotNull(note);
        Assert.False(Directory.Exists(Legacy));
        Assert.Equal("db", File.ReadAllText(Path.Combine(Current, "antigramiq.db")));
        Assert.True(File.Exists(Path.Combine(Current, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Current, "logs", "app-old.log")));
        Assert.Equal("new log", File.ReadAllText(Path.Combine(Current, "logs", "app-new.log")));
    }

    [Fact]
    public void ExistingCurrentFolderIsLeftAloneAndLegacyKept()
    {
        Directory.CreateDirectory(Legacy);
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Legacy, "antibody_panels.db"), "old");
        File.WriteAllText(Path.Combine(Current, "antigramiq.db"), "new");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.Null(note);
        Assert.Equal("old", File.ReadAllText(Path.Combine(Legacy, "antibody_panels.db")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Current, "antigramiq.db")));
    }

    [Fact]
    public void LegacyDatabaseIsNotRenamedOverCurrentDatabase()
    {
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Current, "antibody_panels.db"), "old");
        File.WriteAllText(Path.Combine(Current, "antigramiq.db"), "new");

        Assert.False(AppDataPaths.RenameLegacyDatabase(Current));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Current, "antigramiq.db")));
        Assert.True(File.Exists(Path.Combine(Current, "antibody_panels.db")));
    }

    [Fact]
    public void NothingToMigrateReturnsNull()
    {
        Assert.Null(AppDataPaths.EnsureMigrated(_base));
        Assert.False(Directory.Exists(Current));
    }
}
