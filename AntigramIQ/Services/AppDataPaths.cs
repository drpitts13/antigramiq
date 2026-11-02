using System;
using System.IO;

namespace AntigramIQ.Services
{
    /// <summary>
    /// Per-user data folder (%AppData%\AntigramIQ). On first use, data left by releases
    /// named "AntibodyPanels" is moved here so the database, settings, and logs carry over.
    /// </summary>
    public static class AppDataPaths
    {
        public const string FolderName = "AntigramIQ";
        public const string LegacyFolderName = "AntibodyPanels";
        public const string DatabaseFileName = "antigramiq.db";
        public const string LegacyDatabaseFileName = "antibody_panels.db";

        private static readonly string[] SqliteSuffixes = { "", "-wal", "-shm" };
        private static readonly object Sync = new();
        private static bool _migrated;

        public static string Root
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                lock (Sync)
                {
                    if (!_migrated)
                    {
                        _migrated = true;
                        var note = EnsureMigrated(appData);
                        if (note != null)
                            AppLog.Info(note);
                    }
                }
                return Path.Combine(appData, FolderName);
            }
        }

        /// <summary>
        /// Moves the legacy folder and database file names under <paramref name="appDataBase"/>
        /// to the current names. Never deletes data; on failure the old files stay in place.
        /// Returns a log line describing what moved, or null when nothing was done.
        /// </summary>
        public static string? EnsureMigrated(string appDataBase)
        {
            var current = Path.Combine(appDataBase, FolderName);
            var legacy = Path.Combine(appDataBase, LegacyFolderName);
            string? note = null;

            try
            {
                if (Directory.Exists(legacy))
                {
                    if (!Directory.Exists(current))
                    {
                        Directory.Move(legacy, current);
                        note = $"Moved data folder from {LegacyFolderName} to {FolderName}.";
                    }
                    else if (!File.Exists(Path.Combine(current, DatabaseFileName)))
                    {
                        // The new folder can appear before migration (e.g. a log written first);
                        // carry the legacy files over without replacing anything already there.
                        MergeWithoutOverwrite(legacy, current);
                        note = $"Merged data folder {LegacyFolderName} into {FolderName}.";
                    }
                }

                if (Directory.Exists(current) && RenameLegacyDatabase(current))
                    note = (note == null ? "" : note + " ") +
                           $"Renamed {LegacyDatabaseFileName} to {DatabaseFileName}.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                note = $"Data migration from {LegacyFolderName} skipped: {ex.Message}";
            }

            return note;
        }

        private static void MergeWithoutOverwrite(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
            {
                var target = Path.Combine(to, Path.GetFileName(file));
                if (!File.Exists(target))
                    File.Move(file, target);
            }
            foreach (var dir in Directory.GetDirectories(from))
                MergeWithoutOverwrite(dir, Path.Combine(to, Path.GetFileName(dir)));

            if (Directory.GetFileSystemEntries(from).Length == 0)
                Directory.Delete(from);
        }

        /// <summary>
        /// Renames antibody_panels.db (and its WAL/SHM files) to antigramiq.db in
        /// <paramref name="directory"/> when only the legacy file exists.
        /// </summary>
        public static bool RenameLegacyDatabase(string directory)
        {
            var legacyDb = Path.Combine(directory, LegacyDatabaseFileName);
            var currentDb = Path.Combine(directory, DatabaseFileName);
            if (!File.Exists(legacyDb) || File.Exists(currentDb))
                return false;

            foreach (var suffix in SqliteSuffixes)
            {
                var from = legacyDb + suffix;
                if (File.Exists(from))
                    File.Move(from, currentDb + suffix);
            }
            return true;
        }
    }
}
