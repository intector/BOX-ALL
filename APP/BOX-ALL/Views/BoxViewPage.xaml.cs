using BOX_ALL.ViewModels;
using System.Diagnostics;

namespace BOX_ALL.Views
{
    [QueryProperty(nameof(BoxId), "boxId")]
    public partial class BoxViewPage : ContentPage
    {
        private readonly BoxViewViewModel _viewModel;
        private GridDrawable? _gridDrawable144;
        private GridDrawable96? _gridDrawable96;
        private bool _is96Layout;

        public string? BoxId { get; set; }

        public BoxViewPage(BoxViewViewModel viewModel)
        {
            var sw = Stopwatch.StartNew();

            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;

            Debug.WriteLine($"BoxViewPage constructor: {sw.ElapsedMilliseconds}ms");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var sw = Stopwatch.StartNew();
            Debug.WriteLine("BoxViewPage OnAppearing started");

            // Pass the requested boxId to the ViewModel before loading
            _viewModel.RequestedBoxId = BoxId;

            // Load data (this sets CurrentBox with its type)
            await _viewModel.LoadDataAsync();

            // Select the correct drawable based on box type
            string boxType = _viewModel.CurrentBox?.BoxType ?? "BOXALL144";
            _is96Layout = boxType.Contains("96");

            if (_is96Layout)
            {
                _gridDrawable96 ??= new GridDrawable96();
                GridGraphicsView.Drawable = _gridDrawable96;
                _gridDrawable96.UpdateLocations(_viewModel.Locations);

                // Dimensions are the same (565×587.5), but set explicitly for clarity
                GridGraphicsView.WidthRequest = _gridDrawable96.Width;
                GridGraphicsView.HeightRequest = _gridDrawable96.Height;
            }
            else
            {
                _gridDrawable144 ??= new GridDrawable();
                GridGraphicsView.Drawable = _gridDrawable144;
                _gridDrawable144.UpdateLocations(_viewModel.Locations);

                GridGraphicsView.WidthRequest = _gridDrawable144.Width;
                GridGraphicsView.HeightRequest = _gridDrawable144.Height;
            }

            // Trigger redraw
            GridGraphicsView.Invalidate();

            Debug.WriteLine($"BoxViewPage OnAppearing completed: {sw.ElapsedMilliseconds}ms");
        }

        private async void OnGridTapped(object sender, TappedEventArgs e)
        {
            // Get the tap position relative to the GraphicsView
            var position = e.GetPosition(GridGraphicsView);
            if (!position.HasValue) return;

            // Convert to compartment position using the active drawable
            string? compartmentPosition;
            if (_is96Layout && _gridDrawable96 != null)
                compartmentPosition = _gridDrawable96.GetPositionFromPoint(new PointF((float)position.Value.X, (float)position.Value.Y));
            else if (_gridDrawable144 != null)
                compartmentPosition = _gridDrawable144.GetPositionFromPoint(new PointF((float)position.Value.X, (float)position.Value.Y));
            else
                return;

            if (string.IsNullOrEmpty(compartmentPosition)) return;

            Debug.WriteLine($"Tapped compartment: {compartmentPosition}");

            // Find the location data
            var location = _viewModel.Locations?.FirstOrDefault(l => l.Position == compartmentPosition);

            if (location != null && location.Component != null)
            {
                // Navigate to ComponentDetailsPage with boxId
                var parameters = new Dictionary<string, object>
                {
                    { "componentId", location.ComponentId.ToString() },
                    { "position", compartmentPosition },
                    { "boxId", _viewModel.CurrentBoxId ?? "" }
                };
                await Shell.Current.GoToAsync($"{nameof(ComponentDetailsPage)}", parameters);
            }
            else
            {
                // Empty compartment - offer to add component
                bool result = await DisplayAlert(
                    $"Compartment {compartmentPosition}",
                    "This compartment is empty. Would you like to add a component?",
                    "Add Component", "Cancel");

                if (result)
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "position", compartmentPosition },
                        { "boxId", _viewModel.CurrentBoxId ?? "" }
                    };
                    await Shell.Current.GoToAsync($"{nameof(AddComponentPage)}", parameters);
                }
            }
        }

        private void OnBoxNameDoubleTapped(object sender, TappedEventArgs e)
        {
            _viewModel.StartEditing();
            // Focus the Entry after a brief delay to let it become visible
            Dispatcher.Dispatch(() => BoxNameEntry.Focus());
        }

        private async void OnBoxNameEntryCompleted(object sender, EventArgs e)
        {
            await _viewModel.RenameBoxCommand.ExecuteAsync(null);
        }

        private async void OnBoxNameEntryUnfocused(object sender, FocusEventArgs e)
        {
            // Commit rename when Entry loses focus (user taps elsewhere)
            if (_viewModel.IsEditingName)
            {
                await _viewModel.RenameBoxCommand.ExecuteAsync(null);
            }
        }
    }
}
