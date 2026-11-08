using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>Unsaved grades for one run, kept so a crash or power loss does not lose them.</summary>
    public sealed class ReactionDraft
    {
        public string SpecimenId { get; set; } = string.Empty;
        public int RunId { get; set; }
        public int PanelId { get; set; }
        public string RunDescription { get; set; } = string.Empty;
        public string SavedAtUtc { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public List<ReactionDraftRow> Rows { get; set; } = new();
    }

    public sealed class ReactionDraftRow
    {
        public string Cell { get; set; } = string.Empty;
        public string IS { get; set; } = ReactionGrade.NotTested;
        public string C37 { get; set; } = ReactionGrade.NotTested;
        public string AHG { get; set; } = ReactionGrade.NotTested;
        public string CC { get; set; } = ReactionGrade.NotTested;
        public Dictionary<string, string> Extra { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Recovery file for unsaved reaction grades (one open run at a time). Written on every
    /// edit and removed when the grades are saved or deliberately discarded, so a file still
    /// present at startup means the application did not close normally. A draft is never
    /// saved to the database by itself: restoring it only refills the grid, still unsaved.
    /// </summary>
    public sealed class ReactionDraftStore
    {
        private readonly string _path;

        public ReactionDraftStore(string? directory = null)
        {
            var dir = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HemoIQ", "drafts");
            _path = Path.Combine(dir, "reaction-draft.json");
        }

        public string FilePath => _path;

        public void Save(ReactionDraft draft)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(draft));
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Recovery is best effort; entry and saving still work without it.
            }
        }

        public ReactionDraft? Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var draft = JsonSerializer.Deserialize<ReactionDraft>(File.ReadAllText(_path));
                return draft is { RunId: > 0 } && !string.IsNullOrWhiteSpace(draft.SpecimenId) ? draft : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        public void Delete()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Draft rows whose grades differ from what is saved, with every value checked as a grade.
        /// An unrecognized value is dropped, never restored, so it cannot read as reactive.
        /// </summary>
        public static List<ReactionDraftRow> ChangedRows(ReactionDraft draft, IReadOnlyList<Reaction> saved)
        {
            var byCell = saved.ToDictionary(r => r.CellNumber, StringComparer.OrdinalIgnoreCase);
            var changed = new List<ReactionDraftRow>();
            foreach (var row in draft.Rows)
            {
                var clean = new ReactionDraftRow
                {
                    Cell = row.Cell,
                    IS = GradePaste.Strict(row.IS) ?? ReactionGrade.NotTested,
                    C37 = GradePaste.Strict(row.C37) ?? ReactionGrade.NotTested,
                    AHG = GradePaste.Strict(row.AHG) ?? ReactionGrade.NotTested,
                    CC = GradePaste.Strict(row.CC) ?? ReactionGrade.NotTested,
                };
                foreach (var (phase, value) in row.Extra)
                    clean.Extra[phase] = GradePaste.Strict(value) ?? ReactionGrade.NotTested;

                byCell.TryGetValue(row.Cell, out var s);
                string Saved(string? v) => ReactionGrade.Normalize(v);
                var differs = s == null
                    ? new[] { clean.IS, clean.C37, clean.AHG, clean.CC }.Concat(clean.Extra.Values)
                        .Any(v => v != ReactionGrade.NotTested)
                    : clean.IS != Saved(s.IS) || clean.C37 != Saved(s.C37) || clean.AHG != Saved(s.AHG) ||
                      clean.CC != Saved(s.CC) ||
                      clean.Extra.Any(kv => kv.Value != Saved(s.ExtraPhases.TryGetValue(kv.Key, out var e) ? e : null));
                if (differs) changed.Add(clean);
            }
            return changed;
        }
    }
}
