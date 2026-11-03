using System.Windows;
using HemoIQ.Services;

namespace HemoIQ.Views.Dialogs
{
    public partial class ImportReviewDialog : Window
    {
        public bool ActivateRequested { get; private set; }

        public ImportReviewDialog(PanelImportReview review)
        {
            InitializeComponent();
            ReviewBox.Text = review.Explanation;
        }

        private void ActivateClick(object sender, RoutedEventArgs e)
        {
            ActivateRequested = true;
            DialogResult = true;
        }

        private void KeepInactiveClick(object sender, RoutedEventArgs e)
        {
            ActivateRequested = false;
            DialogResult = true;
        }
    }
}
