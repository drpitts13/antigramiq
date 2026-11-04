using System.Collections.Generic;

namespace HemoIQ.Models
{
    public static class UserRole
    {
        public const string Technologist = "technologist";
        public const string Supervisor = "supervisor";
        public const string Administrator = "administrator";

        public static readonly IReadOnlyList<(string Code, string Label)> Options = new[]
        {
            (Technologist, "Technologist"),
            (Supervisor, "Supervisor"),
            (Administrator, "Administrator"),
        };

        public static bool IsValid(string? code) =>
            code is Technologist or Supervisor or Administrator;

        public static string Label(string? code)
        {
            foreach (var (k, label) in Options)
                if (k == code) return label;
            return code ?? "";
        }
    }

    /// <summary>A registered HemoIQ user, keyed by Windows login.</summary>
    public class AppUser
    {
        public string Login { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = UserRole.Technologist;
        public bool Active { get; set; } = true;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Login : $"{Name} ({Initials})";
        public string RoleLabel => UserRole.Label(Role);
        public string ActiveLabel => Active ? "Active" : "Inactive";
    }
}
