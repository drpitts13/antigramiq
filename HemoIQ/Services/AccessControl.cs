using System;
using System.Collections.Generic;
using System.Linq;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.Services
{
    public enum Permission
    {
        SupervisorReview,
        ClearIdentification,
        ReinstateInvestigation,
        ConfigurePreferences,
        ManageUsers,
    }

    /// <summary>
    /// Role-based permissions over the local user registry. While no active user is
    /// registered, every action is allowed (the behavior before roles existed). Once
    /// users exist, the signed-in Windows login must be a registered, active user whose
    /// role grants the permission.
    /// </summary>
    public static class AccessControl
    {
        /// <summary>Windows login of the person using the application. Tests may replace it.</summary>
        public static string CurrentLogin { get; set; } = Environment.UserName ?? string.Empty;

        private static readonly Dictionary<Permission, string[]> Grants = new()
        {
            [Permission.SupervisorReview] = new[] { UserRole.Supervisor, UserRole.Administrator },
            [Permission.ClearIdentification] = new[] { UserRole.Supervisor, UserRole.Administrator },
            [Permission.ReinstateInvestigation] = new[] { UserRole.Supervisor, UserRole.Administrator },
            [Permission.ConfigurePreferences] = new[] { UserRole.Administrator },
            [Permission.ManageUsers] = new[] { UserRole.Administrator },
        };

        public static string Describe(Permission p) => p switch
        {
            Permission.SupervisorReview => "record a supervisor review",
            Permission.ClearIdentification => "clear a confirmed identification",
            Permission.ReinstateInvestigation => "reinstate a cancelled investigation",
            Permission.ConfigurePreferences => "change Preferences",
            Permission.ManageUsers => "manage users and roles",
            _ => p.ToString(),
        };

        public static bool RegistryEnabled(DatabaseService db) => db.GetUsers().Any(u => u.Active);

        /// <summary>The active registered user for the current login, or null.</summary>
        public static AppUser? CurrentUser(DatabaseService db) =>
            db.GetUsers().FirstOrDefault(u => u.Active &&
                string.Equals(u.Login, CurrentLogin, StringComparison.OrdinalIgnoreCase));

        public static bool Can(DatabaseService db, Permission permission)
        {
            if (!RegistryEnabled(db)) return true;
            var user = CurrentUser(db);
            return user != null && Grants[permission].Contains(user.Role);
        }

        /// <summary>Throws <see cref="UnauthorizedAccessException"/> when the current user lacks the permission.</summary>
        public static void Demand(DatabaseService db, Permission permission)
        {
            if (Can(db, permission)) return;
            var user = CurrentUser(db);
            var who = user == null
                ? $"Windows login '{CurrentLogin}' is not an active HemoIQ user"
                : $"{user.DisplayName} ({UserRole.Label(user.Role)})";
            var allowed = string.Join(" or ", Grants[permission].Select(UserRole.Label));
            throw new UnauthorizedAccessException(
                $"{who} cannot {Describe(permission)}. This requires the {allowed} role.");
        }
    }
}
