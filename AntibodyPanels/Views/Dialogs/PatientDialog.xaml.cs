using System;
using System.Windows;
using AntibodyPanels.Models;

namespace AntibodyPanels.Views.Dialogs
{
    public partial class PatientDialog : Window
    {
        public Patient Result { get; private set; } = new();

        public PatientDialog(string mrn, Patient? existing)
        {
            InitializeComponent();
            MrnBox.Text = mrn;
            DobPicker.DisplayDateEnd = DateTime.Today;
            if (existing == null) return;
            NameBox.Text = existing.Name ?? "";
            if (DateTime.TryParse(existing.DateOfBirth, out var dob)) DobPicker.SelectedDate = dob;
            RequirementsBox.Text = existing.TransfusionRequirements ?? "";
            CommentsBox.Text = existing.BloodBankComments ?? "";
        }

        private static string? Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (DobPicker.SelectedDate?.Date > DateTime.Today)
            {
                MessageBox.Show("Date of birth cannot be in the future.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DobPicker.Focus();
                return;
            }
            Result = new Patient
            {
                Mrn = MrnBox.Text,
                Name = Clean(NameBox.Text),
                DateOfBirth = DobPicker.SelectedDate?.ToString("yyyy-MM-dd"),
                TransfusionRequirements = Clean(RequirementsBox.Text),
                BloodBankComments = Clean(CommentsBox.Text),
            };
            DialogResult = true;
        }
    }
}
