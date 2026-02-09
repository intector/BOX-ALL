using BOX_ALL.Models;
using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    public partial class AddEditBoxPage : ContentPage
    {
        public AddEditBoxPage(AddEditBoxViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is AddEditBoxViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }

        private void OnBoxTypeTapped(object sender, TappedEventArgs e)
        {
            if (BindingContext is AddEditBoxViewModel viewModel)
            {
                // Get the BoxTypeOption from the tapped element's BindingContext
                BoxTypeOption? option = null;

                if (sender is Grid grid)
                    option = grid.BindingContext as BoxTypeOption;
                else if (sender is Border border)
                    option = border.BindingContext as BoxTypeOption;

                if (option != null)
                {
                    viewModel.SelectBoxTypeCommand.Execute(option);
                }
            }
        }
    }
}
