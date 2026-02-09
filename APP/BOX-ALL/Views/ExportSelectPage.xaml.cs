using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    public partial class ExportSelectPage : ContentPage
    {
        public ExportSelectPage(ExportSelectViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is ExportSelectViewModel viewModel)
            {
                await viewModel.LoadBoxesAsync();
            }
        }
    }
}