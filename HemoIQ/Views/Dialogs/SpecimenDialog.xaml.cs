using System;
using System.Windows;
using System.Windows.Controls;
using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.Views.Dialogs
{
    public partial class SpecimenDialog : Window
    {
        public string AccessionNumber => AccessionBox.Text.Trim();
        public string? Mrn => Services.PatientHistory.NormalizeMrn(MrnBox.Text);
        public string? CollectedAt => Combine(CollectedPicker.SelectedDate, CollectedTimeBox.Text);
        public string? ReceivedAt => Combine(ReceivedPicker.SelectedDate, ReceivedTimeBox.Text);
        public string? Collector => CollectorBox.Text;
        public string? CollectionLocation => CollectionLocationBox.Text;
        public string? OrderingLocation => OrderingLocationBox.Text;
        public string? OrderingProvider => OrderingProviderBox.Text;

        private bool _isNew;

        private static string? Combine(DateTime? date, string time)
        {
            if (date == null) return null;
            var t = string.IsNullOrWhiteSpace(time) ? "00:00" : time.Trim();
            return $"{date.Value:yyyy-MM-dd} {t}";
        }

        private static bool ValidTime(string text) =>
            string.IsNullOrWhiteSpace(text) ||
            DateTime.TryParseExact(text.Trim(), new[] { "H:mm", "HH:mm" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);

        private static void Load(DatePicker picker, TextBox timeBox, string? value)
        {
            if (!Services.SpecimenDating.TryParse(value, out var t)) return;
            picker.SelectedDate = t.Date;
            timeBox.Text = t.ToString("HH:mm");
        }
        public string? SourceAccession =>
            SpecimenType == "eluate" && !string.IsNullOrWhiteSpace(SourceBox.Text) ? SourceBox.Text.Trim() : null;
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

        public string PregnancyStatusCode =>
            (PregnancyBox.SelectedItem as TransfusionOption)?.Code ?? Models.PregnancyStatus.Unknown;
        public string? PregnancyEndDate =>
            PregnancyStatusCode == Models.PregnancyStatus.Recent
                ? PregnancyEndPicker.SelectedDate?.ToString("yyyy-MM-dd")
                : null;
        public string? RhigDate => RhigPicker.SelectedDate?.ToString("yyyy-MM-dd");

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
            var pregnancyOptions = new System.Collections.Generic.List<TransfusionOption>();
            foreach (var (code, label) in Models.PregnancyStatus.Options)
                pregnancyOptions.Add(new TransfusionOption(code, label));
            PregnancyBox.ItemsSource = pregnancyOptions;
            PregnancyBox.SelectedIndex = 0;
            PregnancyEndPicker.DisplayDateEnd = DateTime.Today;
            RhigPicker.DisplayDateEnd = DateTime.Today;
            LastTransfusionPicker.DisplayDateEnd = DateTime.Today;

            if (existing == null)
            {
                Title = "Add Specimen";
                _isNew = true;
                ReceivedPicker.SelectedDate = DateTime.Today;
                ReceivedTimeBox.Text = DateTime.Now.ToString("HH:mm");
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
                Load(CollectedPicker, CollectedTimeBox, existing.CollectedAt);
                Load(ReceivedPicker, ReceivedTimeBox, existing.ReceivedAt);
                CollectorBox.Text = existing.Collector ?? "";
                CollectionLocationBox.Text = existing.CollectionLocation ?? "";
                OrderingProviderBox.Text = existing.OrderingProvider ?? "";
                OrderingLocationBox.Text = existing.OrderingLocation ?? "";
                SourceBox.Text = existing.SourceAccession ?? "";
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
                var preg = Models.PregnancyStatus.Normalize(existing.PregnancyStatus);
                for (int i = 0; i < pregnancyOptions.Count; i++)
                    if (pregnancyOptions[i].Code == preg) { PregnancyBox.SelectedIndex = i; break; }
                if (DateTime.TryParse(existing.PregnancyEndDate, out var pe)) PregnancyEndPicker.SelectedDate = pe;
                if (DateTime.TryParse(existing.RhigDate, out var rg)) RhigPicker.SelectedDate = rg;
                if (existing.LastTransfusionDate != null &&
                    DateTime.TryParse(existing.LastTransfusionDate, out var td))
                    LastTransfusionPicker.SelectedDate = td;
            }
            SyncTransfusionDate();
            SyncSource();
            _loaded = true;
            UpdateContextSummary();
        }

        private bool _loaded;

        /// <summary>The dialog's clinical context and serology as an unsaved specimen, for the summary line.</summary>
        private Specimen ContextSnapshot() => new()
        {
            TransfusionStatus = TransfusionStatusCode,
            LastTransfusionDate = LastTransfusionDate,
            PregnancyStatus = PregnancyStatusCode,
            PregnancyEndDate = PregnancyEndDate,
            RhigDate = RhigDate,
            DatResult = DatResult,
            DatIgg = DatIgg,
            DatC3 = DatC3,
            Phenotype = Phenotype,
            Genotype = Genotype,
            PreviousAntibodies = PreviousAntibodies,
        };

        private void UpdateContextSummary()
        {
            if (!_loaded) return;
            ContextSummaryText.Text = SpecimenContextSummary.Describe(ContextSnapshot());
        }

        private void Context_Changed(object sender, RoutedEventArgs e) => UpdateContextSummary();

        /// <summary>Selects and returns the tab that holds <paramref name="field"/>, or null.</summary>
        public TabItem? SelectSectionContaining(FrameworkElement field)
        {
            for (DependencyObject? d = field; d != null;
                 d = LogicalTreeHelper.GetParent(d) ?? System.Windows.Media.VisualTreeHelper.GetParent(d))
                if (d is TabItem tab)
                {
                    Sections.SelectedItem = tab;
                    return tab;
                }
            return null;
        }

        /// <summary>The current context summary line.</summary>
        public string ContextSummary => ContextSummaryText.Text;

        /// <summary>Opens the tab holding <paramref name="field"/>, shows the message, and focuses the field.</summary>
        private void Invalid(string message, FrameworkElement field)
        {
            SelectSectionContaining(field);
            MessageBox.Show(message, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            Dispatcher.BeginInvoke(new Action(() => field.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void CollectedPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // New specimens: date expiration from collection rather than from today.
            if (!_isNew || CollectedPicker.SelectedDate is not DateTime c) return;
            ExpirationPicker.SelectedDate = LabSettings.DefaultExpirationDate(c.Date, AppSettings.Current.DefaultSpecimenDatingDays);
        }

        private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncSource();

        private void SyncSource()
        {
            if (SourceBox == null) return;
            SourceBox.IsEnabled = SpecimenType == "eluate";
        }

        private static void SelectGrade(ComboBox box, string? value)
        {
            box.SelectedIndex = 0;
            for (int i = 0; i < AntigenConstants.DatResults.Count; i++)
                if (string.Equals(AntigenConstants.DatResults[i], value, StringComparison.OrdinalIgnoreCase))
                { box.SelectedIndex = i; break; }
        }

        private void PregnancyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PregnancyEndPicker == null) return;
            PregnancyEndPicker.IsEnabled = PregnancyStatusCode == Models.PregnancyStatus.Recent;
            UpdateContextSummary();
        }

        private void TransfusionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SyncTransfusionDate();
            UpdateContextSummary();
        }

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
                Invalid("Accession number is required.", AccessionBox);
                return;
            }
            if (TransfusionStatusCode == Models.TransfusionStatus.Transfused &&
                LastTransfusionPicker.SelectedDate?.Date > DateTime.Today)
            {
                Invalid("Last transfusion date cannot be in the future.", LastTransfusionPicker);
                return;
            }
            if (PregnancyStatusCode == Models.PregnancyStatus.Recent && PregnancyEndPicker.SelectedDate == null)
            {
                Invalid("Enter the date the recent pregnancy ended.", PregnancyEndPicker);
                return;
            }
            if (PregnancyEndPicker.SelectedDate?.Date > DateTime.Today)
            {
                Invalid("The pregnancy end date cannot be in the future.", PregnancyEndPicker);
                return;
            }
            if (RhigPicker.SelectedDate?.Date > DateTime.Today)
            {
                Invalid("The RhIG date cannot be in the future.", RhigPicker);
                return;
            }
            if (!ValidTime(CollectedTimeBox.Text))
            {
                Invalid("Times must be HH:mm (24-hour), for example 07:45.", CollectedTimeBox);
                return;
            }
            if (!ValidTime(ReceivedTimeBox.Text))
            {
                Invalid("Times must be HH:mm (24-hour), for example 07:45.", ReceivedTimeBox);
                return;
            }
            try
            {
                var now = DateTime.Now;
                var c = Services.SpecimenDating.Normalize(CollectedAt, "Collection time", now);
                var r = Services.SpecimenDating.Normalize(ReceivedAt, "Received time", now);
                if (c != null && r != null && string.CompareOrdinal(r, c) < 0)
                    throw new ArgumentException("Received time cannot be before collection time.");
            }
            catch (ArgumentException ex)
            {
                Invalid(ex.Message, CollectedPicker);
                return;
            }
            DialogResult = true;
        }
    }
}
