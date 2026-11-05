using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class TimelineEvent
    {
        public DateTime When { get; init; }
        /// <summary>True when only the date is known (receipt, run start).</summary>
        public bool DateOnly { get; init; }
        public string What { get; init; } = string.Empty;
        public string Who { get; init; } = string.Empty;

        public string WhenDisplay => DateOnly ? When.ToString("yyyy-MM-dd") : When.ToString("yyyy-MM-dd HH:mm");
        public string WhoDisplay => string.IsNullOrEmpty(Who) ? string.Empty : $" ({Who})";
        public string Display => $"{WhenDisplay}  {What}{WhoDisplay}";
    }

    /// <summary>
    /// Builds one chronological view of an investigation from the specimen
    /// record, panel runs, analysis snapshots, and audit events for the
    /// specimen and its patient record. Read-only.
    /// </summary>
    public static class InvestigationTimeline
    {
        /// <summary>Reaction saves on the same run closer together than this are one entry.</summary>
        public static readonly TimeSpan ReactionSessionGap = TimeSpan.FromMinutes(15);

        public static List<TimelineEvent> Build(DatabaseService db, Specimen s)
        {
            var events = new List<TimelineEvent>();
            if (TryDate(s.CreatedDate, out var received))
                events.Add(new TimelineEvent { When = received, DateOnly = true, What = "Specimen received" });

            var runs = db.GetAllSpecimenRuns(s.AccessionNumber);
            var runNames = runs.ToDictionary(r => r.RunId, r => $"{r.PanelName}, {r.DisplayLabel}");
            foreach (var run in runs)
            {
                if (!TryDate(run.CreatedDate, out var d)) continue;
                events.Add(new TimelineEvent
                {
                    When = d,
                    DateOnly = true,
                    What = run.IsUntreated
                        ? $"Panel run started: {run.PanelName}"
                        : $"Additional testing started: {run.DisplayLabel} on {run.PanelName}",
                });
            }

            foreach (var snap in db.GetAnalysisSnapshots(s.AccessionNumber))
            {
                if (!TryUtc(snap.AnalyzedAtUtc, out var at)) continue;
                events.Add(new TimelineEvent
                {
                    When = at,
                    What = string.IsNullOrEmpty(snap.RuleEngineVersion)
                        ? "Analysis run"
                        : $"Analysis run (rule engine {snap.RuleEngineVersion})",
                    Who = snap.AnalyzedBy ?? string.Empty,
                });
            }

            foreach (var panel in db.GetSpecimenPanels(s.AccessionNumber))
                foreach (var a in db.GetAuditEventsForEntity("panel", panel.PanelId.ToString())
                             .Where(a => a.Action == "edit_panel_typing"))
                    if (TryUtc(a.OccurredAtUtc, out var at))
                    {
                        var count = 0;
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(a.AfterJson ?? "{}");
                            if (doc.RootElement.TryGetProperty("changes", out var c)) count = c.GetArrayLength();
                        }
                        catch (System.Text.Json.JsonException) { }
                        events.Add(new TimelineEvent
                        {
                            When = at,
                            What = $"Panel {panel.Name} (lot {panel.LotNumber}) typing edited: {count} change(s)" +
                                   (string.IsNullOrWhiteSpace(a.Reason) ? "" : $"; reason: {a.Reason}"),
                            Who = a.Operator,
                        });
                    }

            var audit = db.GetAuditEventsForEntity("specimen", s.AccessionNumber);
            events.AddRange(GroupReactionSaves(audit.Where(a => a.Action == "save_reaction"), runNames));
            foreach (var a in audit.Where(a => a.Action != "save_reaction"))
                if (Describe(a) is TimelineEvent e) events.Add(e);

            if (!string.IsNullOrWhiteSpace(s.Mrn))
                foreach (var a in db.GetAuditEventsForEntity("patient", s.Mrn))
                    if (TryUtc(a.OccurredAtUtc, out var at))
                        events.Add(new TimelineEvent
                        {
                            When = at,
                            What = a.Action == "add_patient" ? $"Patient record created for {s.Mrn}" : $"Patient record updated for {s.Mrn}",
                            Who = a.Operator,
                        });

            // Date-only events sort first on their day; ties keep insertion order.
            return events
                .Select((e, i) => (e, i))
                .OrderBy(x => x.e.When.Date)
                .ThenBy(x => x.e.DateOnly ? 0 : 1)
                .ThenBy(x => x.e.When)
                .ThenBy(x => x.i)
                .Select(x => x.e)
                .ToList();
        }

        private static IEnumerable<TimelineEvent> GroupReactionSaves(
            IEnumerable<AuditEvent> saves, Dictionary<int, string> runNames)
        {
            var parsed = saves
                .Select(a => (a, ok: TryUtc(a.OccurredAtUtc, out var at), at, run: RunId(a.AfterJson)))
                .Where(x => x.ok)
                .OrderBy(x => x.at)
                .ToList();
            var result = new List<TimelineEvent>();
            int i = 0;
            while (i < parsed.Count)
            {
                var start = parsed[i];
                var cells = new HashSet<string>();
                int count = 0, changed = 0, j = i;
                var reasons = new List<string>();
                var last = start.at;
                while (j < parsed.Count && parsed[j].run == start.run && parsed[j].at - last <= ReactionSessionGap)
                {
                    count++;
                    if (UserActivityService.IsReactionChange(parsed[j].a)) changed++;
                    if (!string.IsNullOrWhiteSpace(parsed[j].a.Reason) && !reasons.Contains(parsed[j].a.Reason!))
                        reasons.Add(parsed[j].a.Reason!);
                    if (Cell(parsed[j].a.AfterJson) is string c) cells.Add(c);
                    last = parsed[j].at;
                    j++;
                }
                var runText = start.run is int r && runNames.TryGetValue(r, out var n) ? n : "panel";
                result.Add(new TimelineEvent
                {
                    When = start.at,
                    What = $"Reactions entered: {count} save(s), {cells.Count} cell(s) on {runText}" +
                           (changed > 0 ? $"; {changed} recorded result(s) changed" : "") +
                           (reasons.Count > 0 ? $"; reason: {string.Join(" / ", reasons)}" : ""),
                    Who = start.a.Operator,
                });
                i = j;
            }
            return result;
        }

        private static TimelineEvent? Describe(AuditEvent a)
        {
            if (!TryUtc(a.OccurredAtUtc, out var at)) return null;
            string? what = a.Action switch
            {
                "link_panel" => $"Panel attached: {Prop(a.AfterJson, "panel") ?? "panel"}",
                "unlink_panel" => $"Panel removed: {Prop(a.BeforeJson, "panel") ?? "panel"}",
                "confirm_final_call" => $"Identification confirmed: {Prop(a.AfterJson, "antibodies")}" +
                                        (Prop(a.AfterJson, "identifiedBy") is string by ? $" by {by}" : ""),
                "clear_final_call" => $"Identification cleared: {Prop(a.BeforeJson, "FinalAntibodies") ?? "—"}; reason: {a.Reason}",
                "supervisor_review" => $"Supervisor reviewed by {Prop(a.AfterJson, "reviewedBy")}" +
                                       (string.IsNullOrWhiteSpace(a.Reason) ? "" : $"; {a.Reason}"),
                "update_transfusion" => $"Transfusion record set: {TransfusionStatus.Label(Prop(a.AfterJson, "status"))}" +
                                        (Prop(a.AfterJson, "lastTransfusionDate") is string d ? $", last {d}" : ""),
                "update_dat" => $"DAT updated: IgG {Prop(a.AfterJson, "igg") ?? "NT"}, C3 {Prop(a.AfterJson, "c3") ?? "NT"}",
                "update_mrn" => $"MRN set to {Prop(a.AfterJson, "mrn") ?? "(blank)"}",
                "report_output" => DescribeReport(a),
                "update_pregnancy" => $"Pregnancy record set: {PregnancyStatus.Label(Prop(a.AfterJson, "status"))}" +
                                      (Prop(a.AfterJson, "endDate") is string pe ? $", ended {pe}" : "") +
                                      (Prop(a.AfterJson, "rhigDate") is string rh ? $"; RhIG {rh}" : ""),
                "update_collection" => $"Collection/receipt recorded: collected {Prop(a.AfterJson, "collectedAt") ?? "—"}, received {Prop(a.AfterJson, "receivedAt") ?? "—"}",
                "update_origin" => "Origin recorded: " + OriginText(a.AfterJson),
                "clear_reactions" => $"Reactions cleared on {Prop(a.BeforeJson, "panel") ?? "panel"}, {Prop(a.BeforeJson, "run") ?? "run"}: " +
                                     $"{Prop(a.BeforeJson, "gradedCells") ?? "0"} graded cell(s)" +
                                     (string.IsNullOrWhiteSpace(a.Reason) ? "" : $"; reason: {a.Reason}"),
                "delete_run" => $"Run deleted: {Prop(a.BeforeJson, "panel") ?? "panel"}, {Prop(a.BeforeJson, "run") ?? "run"} " +
                                $"with {Prop(a.BeforeJson, "gradedCells") ?? "0"} graded cell(s)" +
                                (string.IsNullOrWhiteSpace(a.Reason) ? "" : $"; reason: {a.Reason}"),
                "cancel_investigation" => $"Investigation cancelled; reason: {a.Reason}",
                "reinstate_investigation" => $"Investigation reinstated; reason: {a.Reason}",
                "link_eluate" => Prop(a.AfterJson, "source") is string src
                    ? $"Linked as eluate of {src}"
                    : "Eluate link removed",
                _ => null,
            };
            return what == null ? null : new TimelineEvent { When = at, What = what, Who = a.Operator };
        }

        private static string DescribeReport(AuditEvent a)
        {
            var label = ReportService.ReportLabel(Prop(a.AfterJson, "reportType"));
            var output = Prop(a.AfterJson, "output");
            var what = output switch
            {
                "print" => $"Report printed: {label}",
                "pdf" => $"Report exported to PDF: {label}",
                "csv" => $"Report exported to CSV: {label}",
                _ => $"Report issued: {label}",
            };
            if (Prop(a.AfterJson, "file") is string f) what += $" ({f})";
            if (Prop(a.AfterJson, "finalAntibodies") is string fa) what += $"; ID at issue: {fa}";
            if (Prop(a.AfterJson, "sha256") is string h && h.Length >= 12) what += $"; fingerprint {h[..12]}";
            return what;
        }

        private static int? RunId(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("runId", out var v) && v.TryGetInt32(out var id) ? id : null;
            }
            catch (JsonException) { return null; }
        }

        private static string? Cell(string? json) => Prop(json, "cellNumber");

        private static string OriginText(string? json)
        {
            var s = new Specimen
            {
                Collector = Prop(json, "collector"),
                CollectionLocation = Prop(json, "collectionLocation"),
                OrderingLocation = Prop(json, "orderingLocation"),
                OrderingProvider = Prop(json, "orderingProvider"),
            };
            return s.OriginDisplay.Length == 0 ? "cleared" : s.OriginDisplay;
        }

        private static string? Prop(string? json, string name)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(name, out var v)) return null;
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Null => null,
                    _ => v.ToString(),
                };
            }
            catch (JsonException) { return null; }
        }

        private static bool TryDate(string? text, out DateTime date) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static bool TryUtc(string? text, out DateTime local)
        {
            local = default;
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
                return false;
            local = utc.ToLocalTime();
            return true;
        }
    }
}
