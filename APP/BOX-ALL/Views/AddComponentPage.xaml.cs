using BOX_ALL.ViewModels;

namespace BOX_ALL.Views
{
    [QueryProperty(nameof(PreselectedPosition), "position")]
    [QueryProperty(nameof(EditMode), "editMode")]
    [QueryProperty(nameof(ComponentId), "componentId")]
    [QueryProperty(nameof(BoxId), "boxId")]
    public partial class AddComponentPage : ContentPage
    {
        private AddComponentViewModel _viewModel;
        private readonly Color _primaryColor = Color.FromArgb("#4A9EFF");
        private readonly Color _mutedColor = Color.FromArgb("#6B7280");

        public string? PreselectedPosition { get; set; }
        public string? EditMode { get; set; }
        public string? ComponentId { get; set; }
        public string? BoxId { get; set; }

        public AddComponentPage(AddComponentViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Pass boxId to the ViewModel
            _viewModel.RequestedBoxId = BoxId;

            _viewModel.ComponentId = ComponentId;

            if (!string.IsNullOrEmpty(PreselectedPosition))
            {
                await _viewModel.InitializeAsync(PreselectedPosition);
            }
            else
            {
                await _viewModel.InitializeAsync();
            }
        }

        private void OnEntryFocused(object sender, FocusEventArgs e)
        {
            var element = sender as VisualElement;
            if (element?.Parent is Frame frame)
            {
                frame.BorderColor = _primaryColor;
            }
        }

        private void OnEntryUnfocused(object sender, FocusEventArgs e)
        {
            var element = sender as VisualElement;
            if (element?.Parent is Frame frame)
            {
                frame.BorderColor = _mutedColor;
            }
        }
    }
}
