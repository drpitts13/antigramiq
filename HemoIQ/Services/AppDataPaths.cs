using System;
using System.IO;

namespace HemoIQ.Services
{
    /// <summary>
    /// Per-user data folder (%AppData%\HemoIQ). On first use, data left by builds named
    /// "AntigramIQ" or "AntibodyPanels" is moved here so the database, settings, and logs carry over.
    /// </summary>
    public static class AppDataPaths
    {
        public const string FolderName = "HemoIQ";
        public const string DatabaseFileName = "hemoiq.db";

        /// <summary>Earlier folder names, newest first. Only the first one found is migrated.</summary>
        public static readonly string[] LegacyFolderNames = { "AntigramIQ", "AntibodyPanels" };

        /// <summary>Earlier database file names, newest first.</summary>
        public static readonly string[] LegacyDatabaseFileNames = { "antigramiq.db", "antibody_panels.db" };

        private static readonly string[] SqliteSuffixes = { "", "-wal", "-shm" };

        public static string Root =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        /// <summary>
        /// Migrates legacy data in the user's %AppData%. Called once from app startup only, so
        /// tests and tools that touch <see cref="Root"/> never move a user's files.
        /// </summary>
        public static void MigrateUserData()
        {
            var note = EnsureMigrated(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (note != null)
                AppLog.Info(note);
        }

        /// <summary>
        /// Moves the newest legacy data folder under <paramref name="appDataBase"/> to the current
        /// name and renames its database. Stops once the current folder has a database, so an
        /// older legacy folder is never mixed in. Never deletes data; on failure the old files
        /// stay in place. Returns a log line describing what moved, or null when nothing was done.
        /// </summary>
        public static string? EnsureMigrated(string appDataBase)
        {
            var current = Path.Combine(appDataBase, FolderName);
            string? note = null;

            try
            {
                foreach (var legacyName in LegacyFolderNames)
                {
                    if (File.Exists(Path.Combine(current, DatabaseFileName)))
                        break;

                    var legacy = Path.Combine(appDataBase, legacyName);
                    if (!Directory.Exists(legacy))
                        continue;

                    if (!Directory.Exists(current))
                    {
                        Directory.Move(legacy, current);
                        note = Append(note, $"Moved data folder from {legacyName} to {FolderName}.");
                    }
                    else
                    {
                        // The new folder can appear before migration (e.g. a log written first);
                        // carry the legacy files over without replacing anything already there.
                        MergeWithoutOverwrite(legacy, current);
                        note = Append(note, $"Merged data folder {legacyName} into {FolderName}.");
                    }

                    var renamed = RenameLegacyDatabase(current);
                    if (renamed != null)
                        note = Append(note, $"Renamed {renamed} to {DatabaseFileName}.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                note = Append(note, $"Data migration to {FolderName} stopped: {ex.Message}");
            }

            return note;
        }

        private static string Append(string? note, string line) =>
            note == null ? line : note + " " + line;

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
        /// Renames the newest legacy database (and its WAL/SHM files) in <paramref name="directory"/>
        /// to hemoiq.db when no hemoiq.db exists. Returns the renamed file name, or null.
        /// </summary>
        public static string? RenameLegacyDatabase(string directory)
        {
            var currentDb = Path.Combine(directory, DatabaseFileName);
            if (File.Exists(currentDb))
                return null;

            foreach (var legacyName in LegacyDatabaseFileNames)
            {
                var legacyDb = Path.Combine(directory, legacyName);
                if (!File.Exists(legacyDb))
                    continue;

                foreach (var suffix in SqliteSuffixes)
                {
                    var from = legacyDb + suffix;
                    if (File.Exists(from))
                        File.Move(from, currentDb + suffix);
                }
                return legacyName;
            }
            return null;
        }
    }
}
