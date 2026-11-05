using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using HemoIQ.Data;
using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.ViewModels
{
    public class AnalyticsViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;
        private readonly MainViewModel? _main;

        public ObservableCollection<AntibodyCountRow> Rows { get; } = new();
        public ObservableCollection<AntibodyTrendPoint> Trend { get; } = new();

        public ICommand Last30DaysCommand { get; }
        public ICommand Last90DaysCommand { get; }
        public ICommand Last12MonthsCommand { get; }
        public ICommand AllTimeCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand OpenOverdueCommand { get; }

        public ObservableCollection<OverdueInvestigation> Overdue { get; } = new();
        public ObservableCollection<TechnologistWorkload> Workload { get; } = new();
        public ObservableCollection<LocationWorkload> LocationWorkload { get; } = new();

        private OverdueInvestigation? _selectedOverdue;
        public OverdueInvestigation? SelectedOverdue
        {
            get => _selectedOverdue;
            set => SetField(ref _selectedOverdue, value);
        }

        private TurnaroundResult _turnaround = new();
        public TurnaroundResult Turnaround
        {
            get => _turnaround;
            private set
            {
                if (!SetField(ref _turnaround, value)) return;
                OnPropertyChanged(nameof(ConfirmMedian));
                OnPropertyChanged(nameof(ConfirmP90));
                OnPropertyChanged(nameof(ReviewMedian));
                OnPropertyChanged(nameof(OverdueLabel));
            }
        }

        public string ConfirmMedian => Turnaround.ReceivedToConfirmed.MedianDisplay("d");
        public string ConfirmP90 =>
            $"90th pct {Turnaround.ReceivedToConfirmed.P90Display("d")} (n={Turnaround.ReceivedToConfirmed.Count})";
        public string ReviewMedian =>
            $"{Turnaround.ConfirmedToReviewed.MedianDisplay("h")} (n={Turnaround.ConfirmedToReviewed.Count})";
        public string OverdueLabel => $"{Turnaround.Overdue.Count} open > {Turnaround.ThresholdDays} d";

        public event EventHandler? ChartDataChanged;

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetField(ref _fromDate, value)) Recompute(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetField(ref _toDate, value)) Recompute(); }
        }

        private int _confirmedSpecimens;
        public int ConfirmedSpecimens
        {
            get => _confirmedSpecimens;
            private set => SetField(ref _confirmedSpecimens, value);
        }

        private int _distinctSpecificities;
        public int DistinctSpecificities
        {
            get => _distinctSpecificities;
            private set => SetField(ref _distinctSpecificities, value);
        }

        private int _totalOccurrences;
        public int TotalOccurrences
        {
            get => _totalOccurrences;
            private set => SetField(ref _totalOccurrences, value);
        }

        private string _mostCommonId = "—";
        public string MostCommonId
        {
            get => _mostCommonId;
            private set => SetField(ref _mostCommonId, value);
        }

        public bool IsAllTime => FromDate == null && ToDate == null;

        public string RangeLabel =>
            IsAllTime
                ? "All time"
                : $"{FromDate?.ToString("yyyy-MM-dd") ?? "…"} to {ToDate?.ToString("yyyy-MM-dd") ?? "…"}";

        public AnalyticsViewModel(DatabaseService db, MainViewModel? main = null)
        {
            _db = db;
            _main = main;
            Last30DaysCommand = new RelayCommand(() => ApplyRange(DateTime.Today.AddDays(-30), DateTime.Today));
            Last90DaysCommand = new RelayCommand(() => ApplyRange(DateTime.Today.AddDays(-90), DateTime.Today));
            Last12MonthsCommand = new RelayCommand(() => ApplyRange(DateTime.Today.AddYears(-1), DateTime.Today));
            AllTimeCommand = new RelayCommand(() => ApplyRange(null, null));
            RefreshCommand = new RelayCommand(Refresh);
            ExportCsvCommand = new RelayCommand(ExportCsv);
            OpenOverdueCommand = new RelayCommand(OpenOverdue, () => SelectedOverdue != null);
            _fromDate = DateTime.Today.AddYears(-1);
            _toDate = DateTime.Today;
            Recompute();
        }

        public void Refresh() => Recompute();

        private void ApplyRange(DateTime? from, DateTime? to)
        {
            _fromDate = from;
            _toDate = to;
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
            Recompute();
        }

        private void Recompute()
        {
            var result = AntibodyAnalyticsService.Compute(_db.GetAllSpecimens(), FromDate, ToDate);

            ConfirmedSpecimens = result.ConfirmedSpecimens;
            DistinctSpecificities = result.DistinctSpecificities;
            TotalOccurrences = result.TotalOccurrences;
            MostCommonId = result.MostCommonId;

            Rows.Clear();
            foreach (var row in result.Rows)
                Rows.Add(row);

            Trend.Clear();
            foreach (var point in result.Trend)
                Trend.Add(point);

            var specimens = _db.GetAllSpecimens();
            Turnaround = TurnaroundService.Compute(specimens, FromDate, ToDate,
                AppSettings.Current.TurnaroundThresholdDays, DateTime.Now,
                s => InvestigationStatusService.Compute(_db, s).Label);
            Overdue.Clear();
            foreach (var o in Turnaround.Overdue) Overdue.Add(o);

            Workload.Clear();
            foreach (var w in WorkloadService.Compute(specimens, FromDate, ToDate, _db.GetUsers())) Workload.Add(w);
            LocationWorkload.Clear();
            foreach (var w in WorkloadService.ComputeByLocation(specimens, FromDate, ToDate)) LocationWorkload.Add(w);

            OnPropertyChanged(nameof(IsAllTime));
            OnPropertyChanged(nameof(RangeLabel));
            ChartDataChanged?.Invoke(this, EventArgs.Empty);
        }

        public static IEnumerable<string> TurnaroundCsvLines(TurnaroundResult t)
        {
            static string N(double? v) => v is double d ? d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "";
            yield return "";
            yield return "Turnaround,Count,Median,90th percentile,Unit";
            yield return $"Received to confirmed,{t.ReceivedToConfirmed.Count},{N(t.ReceivedToConfirmed.Median)},{N(t.ReceivedToConfirmed.Percentile90)},days";
            yield return $"Confirmed to supervisor review,{t.ConfirmedToReviewed.Count},{N(t.ConfirmedToReviewed.Median)},{N(t.ConfirmedToReviewed.Percentile90)},hours";
            yield return "";
            yield return $"Open investigations older than {t.ThresholdDays} days";
            yield return "Accession,Received,Age (days),Status";
            foreach (var o in t.Overdue)
                yield return $"{o.Accession},{o.Received},{o.AgeDays},{o.Status}";
        }

        public static IEnumerable<string> WorkloadCsvLines(IEnumerable<TechnologistWorkload> rows)
        {
            static string Q(string v) => v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
            yield return "";
            yield return "Workload by technologist (confirmations by confirmed date, reviews by review date)";
            yield return "Initials,Name,Confirmed,Multiple antibodies,Supervisor reviews,Median received to confirmed (days)";
            foreach (var w in rows)
                yield return $"{Q(w.Initials)},{Q(w.Name)},{w.Confirmed},{w.MultipleAntibodies},{w.Reviewed}," +
                             (w.MedianConfirmDays is double d ? d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "");
        }

        public static IEnumerable<string> LocationCsvLines(IEnumerable<LocationWorkload> rows)
        {
            static string Q(string v) => v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
            yield return "";
            yield return "Workload by ordering location (investigations received in the range)";
            yield return "Ordering location,Received,Confirmed,Open,Median received to confirmed (days)";
            foreach (var w in rows)
                yield return $"{Q(w.Location)},{w.Received},{w.Confirmed},{w.Open}," +
                             (w.MedianConfirmDays is double d ? d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "");
        }

        private void OpenOverdue()
        {
            if (SelectedOverdue == null || _main == null) return;
            _main.NavigateToWorklistItem(new WorklistItem
            {
                AccessionNumber = SelectedOverdue.Accession,
                TargetTab = "Specimens",
            });
        }

        private void ExportCsv()
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", DefaultExt = "csv" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var result = new AntibodyAnalyticsResult
                {
                    ConfirmedSpecimens = ConfirmedSpecimens,
                    DistinctSpecificities = DistinctSpecificities,
                    TotalOccurrences = TotalOccurrences,
                    MostCommonId = MostCommonId,
                    Rows = Rows.ToList(),
                    Trend = Trend.ToList()
                };
                AntibodyAnalyticsService.ExportToCsv(result, FromDate, ToDate, dlg.FileName);
                File.AppendAllLines(dlg.FileName, TurnaroundCsvLines(Turnaround));
                File.AppendAllLines(dlg.FileName, WorkloadCsvLines(Workload));
                File.AppendAllLines(dlg.FileName, LocationCsvLines(LocationWorkload));
                _main?.SetStatus($"CSV exported: {dlg.FileName}");
                MessageBox.Show("CSV exported successfully.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
