using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    public partial class DashboardPage : ContentPage
    {
        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var viewModel = BindingContext as DashboardViewModel;
            if (viewModel != null)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}