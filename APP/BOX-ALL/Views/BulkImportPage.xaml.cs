using System.IO;
using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    [QueryProperty(nameof(CsvFilePath), "csvFile")]
    public partial class BulkImportPage : ContentPage
    {
        private readonly BulkImportViewModel _viewModel;
        public string? CsvFilePath { get; set; }

        public BulkImportPage(BulkImportViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!string.IsNullOrEmpty(CsvFilePath) && File.Exists(CsvFilePath))
            {
                var fileName = Path.GetFileName(CsvFilePath);
                using var stream = File.OpenRead(CsvFilePath);
                await _viewModel.LoadCsvFileAsync(stream, fileName);
            }
        }
    }
}
