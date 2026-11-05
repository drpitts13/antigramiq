using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    /// <summary>
    /// Finds runs whose reactions were recorded after the panel lot's expiration date.
    /// The recording date of a run is the local date of its last <c>save_reaction</c>
    /// audit entry that carried a graded value; a run with grades but no such entry uses
    /// the run's created date. A REVIEW prompt, not an exclusion: the laboratory decides
    /// whether results from expired reagent cells may be used.
    /// </summary>
    public static class ExpiredReagentCheck
    {
        public sealed record Finding(PanelRun Run, string Expiration, string RecordedOn)
        {
            public string Note =>
                $"Reactions on {Run.PanelName}, {Run.DisplayLabel} were recorded {RecordedOn}, after the lot expired {Expiration}. " +
                "Results from expired reagent red cells should not be used for interpretation without documented review. " +
                "Not an exclusion.";
        }

        public static List<Finding> Check(DatabaseService db, string accessionNumber)
        {
            var findings = new List<Finding>();
            var runs = db.GetAllSpecimenRuns(accessionNumber);
            if (runs.Count == 0) return findings;
            var lastSaveByRun = new Dictionary<int, DateTime>();
            foreach (var a in db.GetAuditEventsForEntity("specimen", accessionNumber).Where(a => a.Action == "save_reaction"))
            {
                if (!TryRun(a.AfterJson, out var runId) ||
                    !DateTime.TryParse(a.OccurredAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                    continue;
                var local = (at.Kind == DateTimeKind.Utc ? at.ToLocalTime() : at).Date;
                if (!lastSaveByRun.TryGetValue(runId, out var prev) || local > prev) lastSaveByRun[runId] = local;
            }

            foreach (var run in runs)
            {
                var panel = db.GetPanel(run.PanelId);
                if (panel?.ExpirationDate == null ||
                    !DateTime.TryParse(panel.ExpirationDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exp))
                    continue;
                if (db.CountGradedReactions(run.RunId) == 0) continue;
                DateTime recorded;
                if (lastSaveByRun.TryGetValue(run.RunId, out var saved)) recorded = saved;
                else if (DateTime.TryParse(run.CreatedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created)) recorded = created.Date;
                else continue;
                if (recorded > exp.Date)
                    findings.Add(new Finding(run, exp.ToString("yyyy-MM-dd"), recorded.ToString("yyyy-MM-dd")));
            }
            return findings;
        }

        private static bool TryRun(string? json, out int runId)
        {
            runId = 0;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("runId", out var r) && r.TryGetInt32(out runId);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
