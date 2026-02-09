using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    public partial class ImportSelectPage : ContentPage
    {
        public ImportSelectPage(ImportSelectViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is ImportSelectViewModel viewModel)
            {
                await viewModel.LoadFilesAsync();
            }
        }
    }
}