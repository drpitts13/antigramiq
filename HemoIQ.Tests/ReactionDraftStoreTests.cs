using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.Tests;

public class ReactionDraftStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hemoiq-draft-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ReactionDraft Draft(params ReactionDraftRow[] rows) => new()
    {
        SpecimenId = "2026-002", RunId = 7, PanelId = 3, RunDescription = "2026-002 (Panel A, Untreated)",
        SavedAtUtc = DateTime.UtcNow.ToString("o"), Operator = "TE", Rows = rows.ToList(),
    };

    [Fact]
    public void SaveLoadDelete_RoundTrips_AndMissingFileIsNull()
    {
        var store = new ReactionDraftStore(_dir);
        Assert.Null(store.Load());
        var row = new ReactionDraftRow { Cell = "1", AHG = "2+" };
        row.Extra["PEG"] = "w+";
        store.Save(Draft(row));

        var loaded = store.Load()!;
        Assert.Equal("2026-002", loaded.SpecimenId);
        Assert.Equal(7, loaded.RunId);
        Assert.Equal("2+", loaded.Rows.Single().AHG);
        Assert.Equal("w+", loaded.Rows.Single().Extra["PEG"]);
        Assert.False(File.Exists(store.FilePath + ".tmp"));

        store.Delete();
        Assert.Null(store.Load());
        store.Delete();
    }

    [Fact]
    public void CorruptOrIncompleteFile_LoadsAsNull()
    {
        var store = new ReactionDraftStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{ not json");
        Assert.Null(store.Load());
        File.WriteAllText(store.FilePath, "{\"SpecimenId\":\"\",\"RunId\":0}");
        Assert.Null(store.Load());
    }

    [Fact]
    public void ChangedRows_KeepsOnlyCellsThatDifferFromSaved_AndDropsUnrecognizedGrades()
    {
        var saved = new List<Reaction>
        {
            new() { CellNumber = "1", IS = "0", C37 = "0", AHG = "2+", CC = "NT" },
            new() { CellNumber = "2", IS = "0", C37 = "0", AHG = "0", CC = "2+" },
        };
        var draft = Draft(
            new ReactionDraftRow { Cell = "1", IS = "0", C37 = "0", AHG = "2+", CC = "NT" },
            new ReactionDraftRow { Cell = "2", IS = "0", C37 = "0", AHG = "3", CC = "2+" },
            new ReactionDraftRow { Cell = "3", AHG = "2=" },
            new ReactionDraftRow { Cell = "4", AHG = "w" });

        var changed = ReactionDraftStore.ChangedRows(draft, saved);
        Assert.Equal(new[] { "2", "4" }, changed.Select(r => r.Cell));
        Assert.Equal("3+", changed[0].AHG);
        Assert.Equal("w+", changed[1].AHG);
        Assert.Empty(ReactionDraftStore.ChangedRows(Draft(new ReactionDraftRow { Cell = "1", IS = "0", C37 = "0", AHG = "2+" }), saved));
    }
}
