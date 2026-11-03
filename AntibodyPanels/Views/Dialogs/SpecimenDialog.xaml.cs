using System;
using System.Windows;
using System.Windows.Controls;
using AntibodyPanels.Models;
using AntibodyPanels.Services;

namespace AntibodyPanels.Views.Dialogs
{
    public partial class SpecimenDialog : Window
    {
        public string AccessionNumber => AccessionBox.Text.Trim();
        public string? Mrn => Services.PatientHistory.NormalizeMrn(MrnBox.Text);
        public string SpecimenType => TypeBox.SelectedItem?.ToString() ?? TypeBox.Text;
        public string? ExpirationDate => ExpirationPicker.SelectedDate?.ToString("yyyy-MM-dd");
        public bool ItemIsActive => ActiveCheck.IsChecked == true;
        public string? Notes => string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();
        public string? Phenotype => string.IsNullOrWhiteSpace(PhenotypeBox.Text) ? null : PhenotypeBox.Text.Trim();
        public string? Genotype => string.IsNullOrWhiteSpace(GenotypeBox.Text) ? null : GenotypeBox.Text.Trim();
        public string? PreviousAntibodies => string.IsNullOrWhiteSpace(PreviousAbsBox.Text) ? null : PreviousAbsBox.Text.Trim();
        public string? DatResult => DatBox.SelectedItem?.ToString();
        public string? DatIgg => DatIggBox.SelectedItem?.ToString();
        public string? DatC3 => DatC3Box.SelectedItem?.ToString();
        public string TransfusionStatusCode =>
            (TransfusionBox.SelectedItem as TransfusionOption)?.Code ?? Models.TransfusionStatus.Unknown;
        public string? LastTransfusionDate =>
            TransfusionStatusCode == Models.TransfusionStatus.Transfused
                ? LastTransfusionPicker.SelectedDate?.ToString("yyyy-MM-dd")
                : null;

        private sealed record TransfusionOption(string Code, string Label);

        public SpecimenDialog(Specimen? existing = null)
        {
            InitializeComponent();

            TypeBox.ItemsSource = AntigenConstants.SpecimenTypes;
            DatBox.ItemsSource = AntigenConstants.DatResults;
            DatIggBox.ItemsSource = AntigenConstants.DatResults;
            DatC3Box.ItemsSource = AntigenConstants.DatResults;
            DatIggBox.SelectedIndex = 0;
            DatC3Box.SelectedIndex = 0;
            var transfusionOptions = new System.Collections.Generic.List<TransfusionOption>();
            foreach (var (code, label) in Models.TransfusionStatus.Options)
                transfusionOptions.Add(new TransfusionOption(code, label));
            TransfusionBox.ItemsSource = transfusionOptions;
            TransfusionBox.SelectedIndex = 0;
            LastTransfusionPicker.DisplayDateEnd = DateTime.Today;

            if (existing == null)
            {
                Title = "Add Specimen";
                var def = AppSettings.Current.DefaultSpecimenType;
                int t = -1;
                for (int i = 0; i < AntigenConstants.SpecimenTypes.Count; i++)
                    if (AntigenConstants.SpecimenTypes[i] == def) { t = i; break; }
                TypeBox.SelectedIndex = t >= 0 ? t : 0;
                DatBox.SelectedIndex = 0;
                ActiveCheck.IsChecked = true;
                ExpirationPicker.SelectedDate = LabSettings.DefaultExpirationDate(
                    DateTime.Today, AppSettings.Current.DefaultSpecimenDatingDays);
            }
            else
            {
                Title = "Edit Specimen";
                AccessionBox.Text = existing.AccessionNumber;
                AccessionBox.IsEnabled = false;
                AccessionBox.Opacity = 0.6;

                var types = AntigenConstants.SpecimenTypes;
                int idx = -1;
                for (int i = 0; i < types.Count; i++) if (types[i] == existing.Type) { idx = i; break; }
                TypeBox.SelectedIndex = idx >= 0 ? idx : 0;

                if (existing.ExpirationDate != null &&
                    DateTime.TryParse(existing.ExpirationDate, out var d))
                    ExpirationPicker.SelectedDate = d;

                ActiveCheck.IsChecked = existing.IsActive;
                MrnBox.Text = existing.Mrn ?? "";
                PhenotypeBox.Text = existing.Phenotype ?? "";
                GenotypeBox.Text = existing.Genotype ?? "";
                PreviousAbsBox.Text = existing.PreviousAntibodies ?? "";
                NotesBox.Text = existing.Notes ?? "";

                int datIdx = 0;
                for (int i = 0; i < AntigenConstants.DatResults.Count; i++)
                    if (AntigenConstants.DatResults[i] == existing.DatResult) { datIdx = i; break; }
                DatBox.SelectedIndex = datIdx;
                SelectGrade(DatIggBox, existing.DatIgg);
                SelectGrade(DatC3Box, existing.DatC3);

                var status = Models.TransfusionStatus.Normalize(existing.TransfusionStatus);
                for (int i = 0; i < transfusionOptions.Count; i++)
                    if (transfusionOptions[i].Code == status) { TransfusionBox.SelectedIndex = i; break; }
                if (existing.LastTransfusionDate != null &&
                    DateTime.TryParse(existing.LastTransfusionDate, out var td))
                    LastTransfusionPicker.SelectedDate = td;
            }
            SyncTransfusionDate();
        }

        private static void SelectGrade(ComboBox box, string? value)
        {
            box.SelectedIndex = 0;
            for (int i = 0; i < AntigenConstants.DatResults.Count; i++)
                if (string.Equals(AntigenConstants.DatResults[i], value, StringComparison.OrdinalIgnoreCase))
                { box.SelectedIndex = i; break; }
        }

        private void TransfusionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            SyncTransfusionDate();

        private void SyncTransfusionDate()
        {
            if (LastTransfusionPicker == null) return;
            LastTransfusionPicker.IsEnabled = TransfusionStatusCode == Models.TransfusionStatus.Transfused;
        }

        private void ExpirationPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExpirationPicker.SelectedDate.HasValue &&
                ExpirationPicker.SelectedDate.Value.Date < DateTime.Today)
            {
                ActiveCheck.IsChecked = false;
            }
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AccessionBox.Text))
            {
                MessageBox.Show("Accession number is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AccessionBox.Focus();
                return;
            }
            if (TransfusionStatusCode == Models.TransfusionStatus.Transfused &&
                LastTransfusionPicker.SelectedDate?.Date > DateTime.Today)
            {
                MessageBox.Show("Last transfusion date cannot be in the future.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                LastTransfusionPicker.Focus();
                return;
            }
            DialogResult = true;
        }
    }
}
