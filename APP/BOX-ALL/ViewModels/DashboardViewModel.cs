using BOX_ALL.Services;
using BOX_ALL.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using BOX_ALL.Models;
using System.Linq;
using System.Threading.Tasks;
using Component = BOX_ALL.Models.Component;

namespace BOX_ALL.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly BoxRegistryService _boxRegistryService;
        private readonly BoxDataService _boxDataService;
        private readonly ImportLogService _importLogService;

        [ObservableProperty]
        private string title = "BOX-ALL Dashboard";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private int totalComponents;

        [ObservableProperty]
        private int locationsUsed;

        [ObservableProperty]
        private int lowStockCount;

        [ObservableProperty]
        private int alertsCount;

        [ObservableProperty]
        private string searchText = "";

        // Empty state properties
        [ObservableProperty]
        private bool hasBoxes = false;

        [ObservableProperty]
        private bool showEmptyState = true;

        // Box selector properties
        [ObservableProperty]
        private ObservableCollection<BoxRegistryItem> boxList = new();

        [ObservableProperty]
        private BoxRegistryItem? selectedBox;

        [ObservableProperty]
        private bool hasSelectedBox = false;

        public DashboardViewModel(
            BoxRegistryService boxRegistryService,
            BoxDataService boxDataService,
            ImportLogService importLogService)
        {
            _boxRegistryService = boxRegistryService;
            _boxDataService = boxDataService;
            _importLogService = importLogService;
        }

        public async Task InitializeAsync()
        {
            ShowEmptyState = true;
            HasBoxes = false;

            await LoadStatistics();
        }

        // When SelectedBox changes, update stats and HasSelectedBox
        partial void OnSelectedBoxChanged(BoxRegistryItem? value)
        {
            HasSelectedBox = value != null;

            if (value != null)
            {
                _ = LoadSelectedBoxStats(value);
            }
        }

        private async Task LoadSelectedBoxStats(BoxRegistryItem box)
        {
            try
            {
                var boxData = await _boxDataService.LoadBoxAsync(box.Id);
                if (boxData != null)
                {
                    var components = boxData.Compartments
                        .Where(c => c.Component != null)
                        .Select(c => c.Component!)
                        .ToList();

                    TotalComponents = components.Sum(c => c.Quantity);
                    LocationsUsed = boxData.Compartments.Count(c => c.Component != null);
                    LowStockCount = components.Count(c => c.Quantity > 0 && c.Quantity <= c.MinStock);
                    AlertsCount = components.Count(c => c.Quantity == 0);
                }
                else
                {
                    TotalComponents = 0;
                    LocationsUsed = 0;
                    LowStockCount = 0;
                    AlertsCount = 0;
                }
            }
            catch
            {
                // Silently handle - stats will show 0
            }
        }

        [RelayCommand]
        private async Task LoadStatistics()
        {
            IsBusy = true;

            try
            {
                var allBoxes = await _boxRegistryService.GetAllBoxesAsync();

                System.Diagnostics.Debug.WriteLine($"Found {allBoxes.Count} boxes");

                HasBoxes = allBoxes.Count > 0;
                ShowEmptyState = !HasBoxes;

                // Update box list for the Picker
                var previousSelectedId = SelectedBox?.Id;
                BoxList.Clear();
                foreach (var box in allBoxes)
                {
                    BoxList.Add(box);
                }

                if (HasBoxes)
                {
                    if (!string.IsNullOrEmpty(previousSelectedId))
                    {
                        SelectedBox = BoxList.FirstOrDefault(b => b.Id == previousSelectedId)
                                      ?? BoxList.FirstOrDefault();
                    }
                    else
                    {
                        SelectedBox = BoxList.FirstOrDefault();
                    }
                }
                else
                {
                    SelectedBox = null;
                    TotalComponents = 0;
                    LocationsUsed = 0;
                    LowStockCount = 0;
                    AlertsCount = 0;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateBox()
        {
            await Shell.Current.GoToAsync(nameof(AddEditBoxPage));
        }

        [RelayCommand]
        private async Task ViewBox()
        {
            if (SelectedBox != null)
            {
                await Shell.Current.GoToAsync($"BoxViewPage?boxId={SelectedBox.Id}");
            }
        }

        [RelayCommand]
        private async Task DeleteBox()
        {
            if (SelectedBox == null) return;

            bool confirm = false;
            if (Application.Current?.Windows?.Count > 0)
            {
                confirm = await Application.Current.Windows[0].Page!.DisplayAlert(
                    "Delete Box",
                    $"Are you sure you want to delete \"{SelectedBox.Name}\"?\n\nThis will permanently remove the box and all its component data.",
                    "Delete", "Cancel");
            }

            if (confirm)
            {
                try
                {
                    IsBusy = true;
                    var boxId = SelectedBox.Id;
                    var success = await _boxRegistryService.DeleteBoxAsync(boxId);

                    if (success)
                    {
                        // Release all import log entries for this box
                        await _importLogService.RemoveEntriesByBoxAsync(boxId);

                        SelectedBox = null;
                        await LoadStatistics();

                        if (Application.Current?.Windows?.Count > 0)
                        {
                            await Application.Current.Windows[0].Page!.DisplayAlert(
                                "Success", "Box deleted successfully.", "OK");
                        }
                    }
                    else
                    {
                        if (Application.Current?.Windows?.Count > 0)
                        {
                            await Application.Current.Windows[0].Page!.DisplayAlert(
                                "Error", "Failed to delete box.", "OK");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (Application.Current?.Windows?.Count > 0)
                    {
                        await Application.Current.Windows[0].Page!.DisplayAlert(
                            "Error", $"Failed to delete box: {ex.Message}", "OK");
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task Search()
        {
            if (!HasBoxes)
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("No Boxes",
                        "Create a storage box first!", "OK");
                }
                return;
            }

            // Search not yet implemented
            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert("Search",
                    "Search feature coming soon!", "OK");
            }
        }
    }
}
