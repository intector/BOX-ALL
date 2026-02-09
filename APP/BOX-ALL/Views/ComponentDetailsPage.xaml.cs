using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    [QueryProperty(nameof(ComponentId), "componentId")]
    [QueryProperty(nameof(Position), "position")]
    [QueryProperty(nameof(BoxId), "boxId")]
    public partial class ComponentDetailsPage : ContentPage
    {
        private ComponentDetailsViewModel _viewModel;

        public string? ComponentId { get; set; }
        public string? Position { get; set; }
        public string? BoxId { get; set; }

        public ComponentDetailsPage(ComponentDetailsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!string.IsNullOrEmpty(ComponentId) && Guid.TryParse(ComponentId, out Guid compId))
            {
                await _viewModel.LoadComponentAsync(compId, Position, BoxId);
            }
        }
    }
}
