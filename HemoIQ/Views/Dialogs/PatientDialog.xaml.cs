using System;
using System.Windows;
using HemoIQ.Models;

namespace HemoIQ.Views.Dialogs
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
            AntigenNegativeBox.Text = existing.AntigenNegative ?? "";
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
            var negative = Services.AntigenNegativeRequirement.Parse(AntigenNegativeBox.Text);
            if (negative.Unrecognized.Count > 0)
            {
                MessageBox.Show($"Not a recognized antigen: {string.Join(", ", negative.Unrecognized)}.\n\n" +
                                "Enter antigen codes such as E, K, Fya or Fy(a-). Case matters for C/c, E/e, K/k, S/s. " +
                                "Write other requirements under Transfusion requirements.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                AntigenNegativeBox.Focus();
                return;
            }
            Result = new Patient
            {
                Mrn = MrnBox.Text,
                Name = Clean(NameBox.Text),
                DateOfBirth = DobPicker.SelectedDate?.ToString("yyyy-MM-dd"),
                TransfusionRequirements = Clean(RequirementsBox.Text),
                AntigenNegative = negative.Canonical,
                BloodBankComments = Clean(CommentsBox.Text),
            };
            DialogResult = true;
        }
    }
}
