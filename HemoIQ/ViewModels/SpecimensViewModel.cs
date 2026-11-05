using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HemoIQ.Data;
using HemoIQ.Models;

namespace HemoIQ.ViewModels
{
    public class SpecimensViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;
        private readonly MainViewModel _main;

        public ObservableCollection<Specimen> Specimens { get; } = new();
        public ObservableCollection<Panel> AllPanels { get; } = new();
        public ObservableCollection<Panel> LinkedPanels { get; } = new();
        public ObservableCollection<SpecimenAntibody> Antibodies { get; } = new();
        public ObservableCollection<SpecimenRuleout> Ruleouts { get; } = new();

        private Specimen? _selectedSpecimen;
        public Specimen? SelectedSpecimen
        {
            get => _selectedSpecimen;
            set
            {
                if (SetField(ref _selectedSpecimen, value))
                    LoadSpecimenDetails();
            }
        }

        private Panel? _selectedLinkedPanel;
        public Panel? SelectedLinkedPanel
        {
            get => _selectedLinkedPanel;
            set => SetField(ref _selectedLinkedPanel, value);
        }

        private bool _showInactive = false;
        public bool ShowInactive
        {
            get => _showInactive;
            set { if (SetField(ref _showInactive, value)) ApplyFilter(); }
        }

        private string _listFilter = string.Empty;
        public string ListFilter
        {
            get => _listFilter;
            set { if (SetField(ref _listFilter, value)) ApplyFilter(); }
        }

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AttachPanelCommand { get; }
        public ICommand DetachPanelCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand EditPatientCommand { get; }
        public ICommand SupervisorReviewCommand { get; }
        public ICommand CancelInvestigationCommand { get; }
        public ICommand ReinstateInvestigationCommand { get; }
        public ICommand OpenReactionsCommand { get; }
        public ICommand OpenAnalysisCommand { get; }
        public ICommand OpenReportCommand { get; }

        public SpecimensViewModel(DatabaseService db, MainViewModel main)
        {
            _db = db;
            _main = main;

            AddCommand = new RelayCommand(AddSpecimen);
            CancelInvestigationCommand = new RelayCommand(CancelInvestigation,
                () => SelectedSpecimen is { IsCancelled: false, HasFinalCall: false });
            ReinstateInvestigationCommand = new RelayCommand(ReinstateInvestigation,
                () => SelectedSpecimen is { IsCancelled: true });
            SupervisorReviewCommand = new RelayCommand(SupervisorReview,
                () => SelectedSpecimen is { HasFinalCall: true, IsReviewed: false });
            EditPatientCommand = new RelayCommand(EditPatient,
                () => !string.IsNullOrWhiteSpace(SelectedSpecimen?.Mrn));
            EditCommand = new RelayCommand(EditSpecimen, () => SelectedSpecimen != null);
            DeleteCommand = new RelayCommand(DeleteSpecimen, () => SelectedSpecimen != null);
            AttachPanelCommand = new RelayCommand(AttachPanel, () => SelectedSpecimen != null);
            DetachPanelCommand = new RelayCommand(DetachPanel,
                () => SelectedSpecimen != null && SelectedLinkedPanel != null);
            RefreshCommand = new RelayCommand(Refresh);
            OpenReactionsCommand = new RelayCommand(OpenReactions, () => SelectedSpecimen != null);
            OpenAnalysisCommand = new RelayCommand(OpenAnalysis, () => SelectedSpecimen != null);
            OpenReportCommand = new RelayCommand(OpenReport, () => SelectedSpecimen != null);

            Refresh();
        }

        private List<Specimen> _allSpecimens = new();

        public void Refresh()
        {
            var selected = SelectedSpecimen?.AccessionNumber;
            _allSpecimens = _db.GetAllSpecimens();
            ApplyFilter(selected);
        }

        public void SelectSpecimen(string accessionNumber)
        {
            EnsureVisible(accessionNumber);
            SelectedSpecimen = Specimens.FirstOrDefault(s => s.AccessionNumber == accessionNumber)
                ?? SelectedSpecimen;
        }

        private void ApplyFilter(string? preferredAccession = null)
        {
            preferredAccession ??= SelectedSpecimen?.AccessionNumber;
            Specimens.Clear();
            foreach (var s in _allSpecimens)
            {
                if (!_showInactive && !s.IsActive) continue;
                if (!s.MatchesFilter(_listFilter)) continue;
                Specimens.Add(s);
            }
            SelectedSpecimen = Specimens.FirstOrDefault(s => s.AccessionNumber == preferredAccession)
                ?? Specimens.FirstOrDefault();
        }

        private void EnsureVisible(string accessionNumber)
        {
            var match = _allSpecimens.FirstOrDefault(s => s.AccessionNumber == accessionNumber);
            if (match == null) return;
            if (!_showInactive && !match.IsActive)
            {
                _showInactive = true;
                OnPropertyChanged(nameof(ShowInactive));
            }
            if (!match.MatchesFilter(_listFilter))
            {
                _listFilter = string.Empty;
                OnPropertyChanged(nameof(ListFilter));
            }
            ApplyFilter(accessionNumber);
        }

        public ObservableCollection<Services.TimelineEvent> Timeline { get; } = new();

        private Services.InvestigationStatus? _status;
        /// <summary>Derived investigation status for the selected specimen.</summary>
        public Services.InvestigationStatus? Status
        {
            get => _status;
            private set => SetField(ref _status, value);
        }

        private string _patientText = string.Empty;
        /// <summary>Permanent patient information for the selected specimen's MRN.</summary>
        public string PatientText
        {
            get => _patientText;
            private set => SetField(ref _patientText, value);
        }

        private void CancelInvestigation()
        {
            var s = SelectedSpecimen;
            if (s == null) return;
            var dlg = new Views.Dialogs.ReasonDialog(
                $"Cancel investigation {s.AccessionNumber}? The record is kept and locked, and it leaves the work queues.\n" +
                "Enter the reason (for example: mislabeled specimen, hemolyzed, order cancelled).");
            if (dlg.ShowDialog() != true) return;
            try
            {
                _db.CancelInvestigation(s.AccessionNumber, dlg.Reason);
                _main.SetStatus($"Investigation {s.AccessionNumber} cancelled.");
                NotifySpecimensChanged();
            }
            catch (System.Exception ex) when (ex is System.ArgumentException or System.InvalidOperationException or System.UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Cancel investigation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ReinstateInvestigation()
        {
            var s = SelectedSpecimen;
            if (s == null) return;
            var dlg = new Views.Dialogs.ReasonDialog(
                $"Reinstate investigation {s.AccessionNumber} (cancelled {s.CancelledAt}: {s.CancelReason})?\nEnter the reason.");
            if (dlg.ShowDialog() != true) return;
            try
            {
                _db.ReinstateInvestigation(s.AccessionNumber, dlg.Reason);
                _main.SetStatus($"Investigation {s.AccessionNumber} reinstated.");
                NotifySpecimensChanged();
            }
            catch (System.Exception ex) when (ex is System.ArgumentException or System.InvalidOperationException or System.UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Reinstate investigation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SupervisorReview()
        {
            var s = SelectedSpecimen;
            if (s == null) return;
            var dlg = new Views.Dialogs.ReasonDialog(
                $"Supervisor review of {s.AccessionNumber}: confirmed {s.FinalAntibodies} by {s.IdentifiedBy ?? "—"}.\n" +
                "Enter your initials. You must not be the person who confirmed the identification.");
            if (dlg.ShowDialog() != true) return;
            try
            {
                _db.SetSpecimenSupervisorReview(s.AccessionNumber, dlg.Reason);
                _main.SetStatus($"Supervisor review recorded for {s.AccessionNumber}.");
                NotifySpecimensChanged();
            }
            catch (System.Exception ex) when (ex is System.ArgumentException or System.InvalidOperationException or System.UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Supervisor review", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EditPatient()
        {
            var mrn = SelectedSpecimen?.Mrn;
            if (string.IsNullOrWhiteSpace(mrn)) return;
            var dlg = new Views.Dialogs.PatientDialog(mrn, _db.GetPatient(mrn));
            if (dlg.ShowDialog() != true) return;
            try
            {
                _db.SavePatient(dlg.Result);
                _main.SetStatus($"Patient {mrn} saved.");
                LoadSpecimenDetails();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error saving patient: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static string FormatPatient(Patient? p, string? mrn)
        {
            if (string.IsNullOrWhiteSpace(mrn)) return "No MRN recorded.";
            if (p == null) return $"No patient record for MRN {mrn}. Use Edit Patient to add one.";
            var lines = new System.Collections.Generic.List<string> { p.Display };
            lines.Add($"Antigen-negative units: {Services.AntigenNegativeRequirement.Display(p.AntigenNegative)}");
            lines.Add($"Transfusion requirements: {p.TransfusionRequirements ?? "None recorded"}");
            if (!string.IsNullOrWhiteSpace(p.BloodBankComments))
                lines.Add($"Blood bank comments: {p.BloodBankComments}");
            return string.Join(System.Environment.NewLine, lines);
        }

        private string _datingText = string.Empty;
        /// <summary>Collection/receipt times and any specimen-dating review note.</summary>
        public string DatingText
        {
            get => _datingText;
            private set => SetField(ref _datingText, value);
        }

        private string _eluateLinkText = string.Empty;
        /// <summary>"Eluate of X" for an eluate, or "Eluates: A, B" for a source specimen.</summary>
        public string EluateLinkText
        {
            get => _eluateLinkText;
            private set => SetField(ref _eluateLinkText, value);
        }

        private string _patientHistoryText = string.Empty;
        /// <summary>Other specimens with the same MRN, newest first.</summary>
        public string PatientHistoryText
        {
            get => _patientHistoryText;
            private set => SetField(ref _patientHistoryText, value);
        }

        private void LoadSpecimenDetails()
        {
            PatientHistoryText = string.Empty;
            PatientText = string.Empty;
            Status = null;
            Timeline.Clear();
            LinkedPanels.Clear();
            Antibodies.Clear();
            Ruleouts.Clear();

            if (_selectedSpecimen == null) return;
            var sid = _selectedSpecimen.AccessionNumber;

            foreach (var p in _db.GetSpecimenPanels(sid)) LinkedPanels.Add(p);
            foreach (var a in _db.GetSpecimenAntibodies(sid)) Antibodies.Add(a);
            foreach (var r in _db.GetSpecimenRuleouts(sid)) Ruleouts.Add(r);

            PatientText = FormatPatient(_db.GetPatient(_selectedSpecimen.Mrn), _selectedSpecimen.Mrn);
            Status = Services.InvestigationStatusService.Compute(_db, _selectedSpecimen);
            var times = $"Collected {_selectedSpecimen.CollectedAt ?? "not recorded"}; received {_selectedSpecimen.ReceivedAt ?? _selectedSpecimen.CreatedDate}." +
                        (_selectedSpecimen.OriginDisplay.Length == 0 ? "" : $" {_selectedSpecimen.OriginDisplay}.");
            DatingText = Services.SpecimenDating.Check(_selectedSpecimen, Services.AppSettings.Current.DefaultSpecimenDatingDays,
                Services.AppSettings.Current.DatingTransfusionLookbackDays) is string note
                ? times + " REVIEW: " + note
                : times;
            var eluates = _db.GetEluatesFor(sid);
            EluateLinkText = _selectedSpecimen.SourceAccession is string src
                ? $"Eluate of {src}"
                : eluates.Count > 0 ? "Eluates: " + string.Join(", ", eluates.Select(e => e.AccessionNumber)) : "None";
            foreach (var e in Services.InvestigationTimeline.Build(_db, _selectedSpecimen)) Timeline.Add(e);
            if (string.IsNullOrWhiteSpace(_selectedSpecimen.Mrn))
                PatientHistoryText = "No MRN recorded; history from other specimens is not linked.";
            else
            {
                var lines = Services.PatientHistory.Describe(
                    Services.PatientHistory.OtherSpecimens(_db, _selectedSpecimen));
                PatientHistoryText = lines.Count == 0
                    ? $"No other specimens for MRN {_selectedSpecimen.Mrn}."
                    : string.Join(System.Environment.NewLine, lines);
            }
        }

        /// <summary>
        /// After any specimen CRUD, refresh both this tab and any other tabs that hold a specimen list.
        /// </summary>
        private void NotifySpecimensChanged()
        {
            Refresh();
            _main.ReactionsVM.RefreshSpecimens();
            _main.AnalysisVM.Refresh();
            _main.ReportsVM.Refresh();
            _main.WorklistVM.Refresh();
        }

        private void AddSpecimen()
        {
            var dlg = new Views.Dialogs.SpecimenDialog();
            if (dlg.ShowDialog() != true) return;
            var existing = _db.GetSpecimen(dlg.AccessionNumber);
            if (existing != null)
            {
                var open = MessageBox.Show(
                    $"Accession {dlg.AccessionNumber} already exists. Open that specimen instead?",
                    "Duplicate accession", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes)
                    SelectSpecimen(dlg.AccessionNumber);
                return;
            }
            try
            {
                Services.SpecimenSaveService.Save(_db, ToEdit(dlg, dlg.AccessionNumber), isNew: true);
                _main.SetStatus($"Specimen {dlg.AccessionNumber} added.");
                NotifySpecimensChanged();
                SelectedSpecimen = Specimens.FirstOrDefault(s => s.AccessionNumber == dlg.AccessionNumber);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"The specimen was not added. Nothing was saved.\n\n{ex.Message}", "Specimen not saved",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Services.SpecimenEdit ToEdit(Views.Dialogs.SpecimenDialog dlg, string accession) => new()
        {
            AccessionNumber = accession,
            Type = dlg.SpecimenType,
            ExpirationDate = dlg.ExpirationDate,
            IsActive = dlg.ItemIsActive,
            Notes = dlg.Notes,
            Phenotype = dlg.Phenotype,
            Genotype = dlg.Genotype,
            PreviousAntibodies = dlg.PreviousAntibodies,
            DatResult = dlg.DatResult,
            DatIgg = dlg.DatIgg,
            DatC3 = dlg.DatC3,
            TransfusionStatus = dlg.TransfusionStatusCode,
            LastTransfusionDate = dlg.LastTransfusionDate,
            Mrn = dlg.Mrn,
            SourceAccession = dlg.SourceAccession,
            CollectedAt = dlg.CollectedAt,
            ReceivedAt = dlg.ReceivedAt,
            Collector = dlg.Collector,
            CollectionLocation = dlg.CollectionLocation,
            OrderingLocation = dlg.OrderingLocation,
            OrderingProvider = dlg.OrderingProvider,
            PregnancyStatus = dlg.PregnancyStatusCode,
            PregnancyEndDate = dlg.PregnancyEndDate,
            RhigDate = dlg.RhigDate,
        };

        private void EditSpecimen()
        {
            if (SelectedSpecimen == null) return;
            var dlg = new Views.Dialogs.SpecimenDialog(SelectedSpecimen);
            if (dlg.ShowDialog() != true) return;
            try
            {
                Services.SpecimenSaveService.Save(_db, ToEdit(dlg, SelectedSpecimen.AccessionNumber), isNew: false);
                _main.SetStatus($"Specimen {SelectedSpecimen.AccessionNumber} updated.");
                NotifySpecimensChanged();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"No changes were saved.\n\n{ex.Message}", "Specimen not saved",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSpecimen()
        {
            if (SelectedSpecimen == null) return;
            var acc = SelectedSpecimen.AccessionNumber;
            if (MessageBox.Show($"Delete specimen {acc}? This will also delete all reactions and analysis results.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                _db.DeleteSpecimen(acc);
            }
            catch (System.InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Specimen not deleted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _main.SetStatus($"Specimen {acc} deleted.");
            NotifySpecimensChanged();
        }

        private void OpenReactions()
        {
            if (SelectedSpecimen == null) return;
            _main.OpenSpecimenReactions(SelectedSpecimen.AccessionNumber);
        }

        private void OpenAnalysis()
        {
            if (SelectedSpecimen == null) return;
            _main.OpenSpecimenAnalysis(SelectedSpecimen.AccessionNumber);
        }

        private void OpenReport()
        {
            if (SelectedSpecimen == null) return;
            _main.OpenSpecimenReport(SelectedSpecimen.AccessionNumber);
        }

        private void AttachPanel()
        {
            if (SelectedSpecimen == null) return;
            var allPanels = _db.GetAllPanels();
            var linked = _db.GetSpecimenPanels(SelectedSpecimen.AccessionNumber)
                .Select(p => p.PanelId).ToHashSet();
            var available = allPanels.Where(p => !linked.Contains(p.PanelId) && p.IsActive).ToList();

            var dlg = new Views.Dialogs.SelectPanelDialog(available);
            if (dlg.ShowDialog() != true || dlg.SelectedPanel == null) return;
            _db.LinkSpecimenPanel(SelectedSpecimen.AccessionNumber, dlg.SelectedPanel.PanelId);
            _main.SetStatus($"Panel '{dlg.SelectedPanel.Name}' attached.");
            LoadSpecimenDetails();
            _main.ReactionsVM.RefreshSpecimens();
        }

        private void DetachPanel()
        {
            if (SelectedSpecimen == null || SelectedLinkedPanel == null) return;
            if (MessageBox.Show($"Detach panel '{SelectedLinkedPanel.Name}'?",
                "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _db.UnlinkSpecimenPanel(SelectedSpecimen.AccessionNumber, SelectedLinkedPanel.PanelId);
            _main.SetStatus("Panel detached.");
            LoadSpecimenDetails();
            _main.ReactionsVM.RefreshSpecimens();
        }
    }
}
