using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public sealed class UserActivity
    {
        public string Login { get; init; } = string.Empty;
        /// <summary>Registered user's display name, or empty for an unregistered login.</summary>
        public string Name { get; init; } = string.Empty;
        public int ReactionEntries { get; init; }
        public int ReactionChanges { get; init; }
        public int Specimens { get; init; }
        public int Confirmations { get; init; }
        public int Clears { get; init; }
        public int Reviews { get; init; }

        public string Display => string.IsNullOrWhiteSpace(Name) ? Login : $"{Name} [{Login}]";
        public int Total => ReactionEntries + ReactionChanges + Confirmations + Clears + Reviews;
    }

    /// <summary>
    /// Work per Windows login from the audit log within a date range (local date of the
    /// event): reaction cells entered, recorded results changed, specimens worked on,
    /// identifications confirmed, identifications cleared (amendments), and supervisor
    /// reviews. Unlike initials-based workload, this counts reaction entry and amendments.
    /// Saves that changed nothing are not audited, so they are not counted.
    /// </summary>
    public static class UserActivityService
    {
        public static readonly string[] Actions =
            { "save_reaction", "confirm_final_call", "clear_final_call", "supervisor_review" };

        public static List<UserActivity> Compute(DatabaseService db, DateTime? from, DateTime? to) =>
            Compute(db.GetAuditEventsByAction(Actions), from, to, db.GetUsers());

        public static List<UserActivity> Compute(IEnumerable<AuditEvent> events, DateTime? from, DateTime? to,
            IEnumerable<AppUser>? users = null)
        {
            var names = (users ?? Enumerable.Empty<AppUser>())
                .GroupBy(u => u.Login, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);

            return events
                .Where(e => InRange(e.OccurredAtUtc, from, to))
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Operator) ? "(unknown)" : e.Operator.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var reactions = g.Where(e => e.Action == "save_reaction").ToList();
                    return new UserActivity
                    {
                        Login = g.Key,
                        Name = names.GetValueOrDefault(g.Key) ?? string.Empty,
                        ReactionEntries = reactions.Count(e => !IsReactionChange(e)),
                        ReactionChanges = reactions.Count(IsReactionChange),
                        Specimens = g.Where(e => e.EntityType == "specimen" && e.EntityId != null)
                            .Select(e => e.EntityId!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        Confirmations = g.Count(e => e.Action == "confirm_final_call"),
                        Clears = g.Count(e => e.Action == "clear_final_call"),
                        Reviews = g.Count(e => e.Action == "supervisor_review"),
                    };
                })
                .OrderByDescending(a => a.Total)
                .ThenBy(a => a.Login, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// True when a reaction save replaced a recorded result: the cell already had a
        /// graded value (anything other than NT or blank) in at least one phase.
        /// </summary>
        public static bool IsReactionChange(AuditEvent e)
        {
            if (e.Action != "save_reaction" || string.IsNullOrWhiteSpace(e.BeforeJson)) return false;
            try
            {
                using var doc = JsonDocument.Parse(e.BeforeJson);
                foreach (var name in new[] { "is_", "c37", "ahg", "cc" })
                    if (doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String &&
                        v.GetString() is string s && s.Length > 0 && !string.Equals(s, "NT", StringComparison.OrdinalIgnoreCase))
                        return true;
                return doc.RootElement.TryGetProperty("extra", out var x) && x.ValueKind == JsonValueKind.String &&
                       !string.IsNullOrWhiteSpace(x.GetString());
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool InRange(string utcText, DateTime? from, DateTime? to)
        {
            if (!DateTime.TryParse(utcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc))
                return false;
            var local = (utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc).Date;
            return (from == null || local >= from.Value.Date) && (to == null || local <= to.Value.Date);
        }
    }
}
