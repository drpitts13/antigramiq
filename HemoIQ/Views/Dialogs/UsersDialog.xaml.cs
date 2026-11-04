using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HemoIQ.Data;
using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.Views.Dialogs
{
    public partial class UsersDialog : Window
    {
        private readonly DatabaseService _db;
        private sealed record RoleOption(string Code, string Label);

        public UsersDialog(DatabaseService db)
        {
            _db = db;
            InitializeComponent();
            RoleBox.ItemsSource = UserRole.Options.Select(o => new RoleOption(o.Code, o.Label)).ToList();
            RoleBox.SelectedIndex = AccessControl.RegistryEnabled(db) ? 0 : 2; // first user: Administrator
            Reload();
        }

        private void Reload()
        {
            UsersGrid.ItemsSource = _db.GetUsers();
            var me = AccessControl.CurrentUser(_db);
            CurrentUserText.Text = !AccessControl.RegistryEnabled(_db)
                ? $"No users registered. Signed in as Windows login '{AccessControl.CurrentLogin}'."
                : me == null
                    ? $"Windows login '{AccessControl.CurrentLogin}' is not an active user."
                    : $"Signed in as {me.DisplayName}, {me.RoleLabel}.";
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not AppUser u) return;
            LoginBox.Text = u.Login;
            InitialsBox.Text = u.Initials;
            NameBox.Text = u.Name;
            RoleBox.SelectedItem = ((System.Collections.Generic.List<RoleOption>)RoleBox.ItemsSource)
                .FirstOrDefault(r => r.Code == u.Role);
            ActiveCheck.IsChecked = u.Active;
        }

        private void UseMyLogin_Click(object sender, RoutedEventArgs e) => LoginBox.Text = AccessControl.CurrentLogin;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _db.SaveUser(new AppUser
                {
                    Login = LoginBox.Text,
                    Initials = InitialsBox.Text,
                    Name = NameBox.Text,
                    Role = (RoleBox.SelectedItem as RoleOption)?.Code ?? UserRole.Technologist,
                    Active = ActiveCheck.IsChecked == true,
                });
                Reload();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Users and roles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
