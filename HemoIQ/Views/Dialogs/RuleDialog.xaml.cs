using System.Linq;
using System.Windows;
using HemoIQ.Models;

namespace HemoIQ.Views.Dialogs
{
    public partial class RuleDialog : Window
    {
        public string RuleName => NameBox.Text.Trim();
        public string? Description => string.IsNullOrWhiteSpace(DescBox.Text) ? null : DescBox.Text.Trim();
        public string Antibody => AntibodyBox.Text.Trim();
        public string? ExceptionAntigen => string.IsNullOrWhiteSpace(ExceptionAntigenBox.Text) ? null : ExceptionAntigenBox.Text.Trim();
        public bool HeterozygousOk => HeterozygousCheck.IsChecked == true;
        public int MinRuleoutCount => int.TryParse(MinCountBox.Text, out var n) ? n : 3;
        public bool SeparationExempt => SeparationExemptCheck.IsChecked == true;
        public string? SeparationExemptFrom => SeparationExempt && !string.IsNullOrWhiteSpace(SeparationFromBox.Text)
            ? SeparationFromBox.Text.Trim() : null;

        public RuleDialog(Rule? existing = null)
        {
            InitializeComponent();
            var antibodies = AntigenConstants.AllKnownAntigens.Select(ag => $"anti-{ag}").ToList();
            AntibodyBox.ItemsSource = antibodies;
            ExceptionAntigenBox.ItemsSource = AntigenConstants.AllKnownAntigens.ToList();

            if (existing != null)
            {
                NameBox.Text = existing.Name;
                AntibodyBox.Text = existing.Antibody;
                DescBox.Text = existing.Description;
                ExceptionAntigenBox.Text = existing.ExceptionAntigen;
                HeterozygousCheck.IsChecked = existing.HeterozygousOk;
                MinCountBox.Text = existing.MinRuleoutCount.ToString();
                SeparationExemptCheck.IsChecked = existing.SeparationExempt;
                SeparationFromBox.Text = existing.SeparationExemptFrom;
            }
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(AntibodyBox.Text))
            {
                MessageBox.Show("Name and Antibody are required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var unknown = Services.MultipleAntibodySeparation.ParseAntigenList(SeparationExemptFrom)
                .Where(a => !AntigenConstants.IsKnown(a)).ToList();
            if (unknown.Count > 0)
            {
                MessageBox.Show($"Unknown antigen(s) in Exempt from: {string.Join(", ", unknown)}. " +
                    "Antigen names are case-sensitive (E and e differ).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
    }
}
