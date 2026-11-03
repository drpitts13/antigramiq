using HemoIQ.Services;

namespace HemoIQ.Tests;

public sealed class AppDataMigrationTests : IDisposable
{
    private readonly string _base =
        Path.Combine(Path.GetTempPath(), $"hemoiq_migration_{Guid.NewGuid():N}");

    public AppDataMigrationTests() => Directory.CreateDirectory(_base);

    public void Dispose()
    {
        try { Directory.Delete(_base, recursive: true); } catch { /* best effort cleanup */ }
    }

    private string AntibodyPanels => Path.Combine(_base, "AntibodyPanels");
    private string AntigramIq => Path.Combine(_base, "AntigramIQ");
    private string Current => Path.Combine(_base, AppDataPaths.FolderName);

    [Fact]
    public void AntibodyPanelsFolderIsMovedAndDatabaseRenamed()
    {
        Directory.CreateDirectory(Path.Combine(AntibodyPanels, "logs"));
        File.WriteAllText(Path.Combine(AntibodyPanels, "antibody_panels.db"), "db");
        File.WriteAllText(Path.Combine(AntibodyPanels, "antibody_panels.db-wal"), "wal");
        File.WriteAllText(Path.Combine(AntibodyPanels, "antibody_panels.db-shm"), "shm");
        File.WriteAllText(Path.Combine(AntibodyPanels, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(AntibodyPanels, "logs", "app.log"), "log");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.NotNull(note);
        Assert.False(Directory.Exists(AntibodyPanels));
        Assert.Equal("db", File.ReadAllText(Path.Combine(Current, "hemoiq.db")));
        Assert.Equal("wal", File.ReadAllText(Path.Combine(Current, "hemoiq.db-wal")));
        Assert.Equal("shm", File.ReadAllText(Path.Combine(Current, "hemoiq.db-shm")));
        Assert.True(File.Exists(Path.Combine(Current, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Current, "logs", "app.log")));
        Assert.False(File.Exists(Path.Combine(Current, "antibody_panels.db")));
    }

    [Fact]
    public void AntigramIqFolderWinsOverOlderAntibodyPanelsFolder()
    {
        Directory.CreateDirectory(AntigramIq);
        Directory.CreateDirectory(AntibodyPanels);
        File.WriteAllText(Path.Combine(AntigramIq, "antigramiq.db"), "newer");
        File.WriteAllText(Path.Combine(AntigramIq, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(AntibodyPanels, "antibody_panels.db"), "older");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.NotNull(note);
        Assert.False(Directory.Exists(AntigramIq));
        Assert.Equal("newer", File.ReadAllText(Path.Combine(Current, "hemoiq.db")));
        Assert.True(File.Exists(Path.Combine(Current, "settings.json")));
        Assert.False(File.Exists(Path.Combine(Current, "antibody_panels.db")));
        Assert.Equal("older", File.ReadAllText(Path.Combine(AntibodyPanels, "antibody_panels.db")));
    }

    [Fact]
    public void LegacyDataMergesIntoCurrentFolderThatHasNoDatabase()
    {
        Directory.CreateDirectory(Path.Combine(AntigramIq, "logs"));
        Directory.CreateDirectory(Path.Combine(Current, "logs"));
        File.WriteAllText(Path.Combine(AntigramIq, "antigramiq.db"), "db");
        File.WriteAllText(Path.Combine(AntigramIq, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(AntigramIq, "logs", "app-old.log"), "old log");
        File.WriteAllText(Path.Combine(Current, "logs", "app-new.log"), "new log");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.NotNull(note);
        Assert.False(Directory.Exists(AntigramIq));
        Assert.Equal("db", File.ReadAllText(Path.Combine(Current, "hemoiq.db")));
        Assert.True(File.Exists(Path.Combine(Current, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Current, "logs", "app-old.log")));
        Assert.Equal("new log", File.ReadAllText(Path.Combine(Current, "logs", "app-new.log")));
    }

    [Fact]
    public void ExistingCurrentDatabaseIsLeftAloneAndLegacyKept()
    {
        Directory.CreateDirectory(AntibodyPanels);
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(AntibodyPanels, "antibody_panels.db"), "old");
        File.WriteAllText(Path.Combine(Current, "hemoiq.db"), "new");

        var note = AppDataPaths.EnsureMigrated(_base);

        Assert.Null(note);
        Assert.Equal("old", File.ReadAllText(Path.Combine(AntibodyPanels, "antibody_panels.db")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Current, "hemoiq.db")));
    }

    [Fact]
    public void LegacyDatabaseIsNotRenamedOverCurrentDatabase()
    {
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Current, "antigramiq.db"), "old");
        File.WriteAllText(Path.Combine(Current, "hemoiq.db"), "new");

        Assert.Null(AppDataPaths.RenameLegacyDatabase(Current));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Current, "hemoiq.db")));
        Assert.True(File.Exists(Path.Combine(Current, "antigramiq.db")));
    }

    [Fact]
    public void NothingToMigrateReturnsNull()
    {
        Assert.Null(AppDataPaths.EnsureMigrated(_base));
        Assert.False(Directory.Exists(Current));
    }
}
