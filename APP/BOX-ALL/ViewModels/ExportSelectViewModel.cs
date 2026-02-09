using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace BOX_ALL.ViewModels
{
    public partial class ExportSelectViewModel : ObservableObject
    {
        private readonly ExportImportService _exportImportService;

        [ObservableProperty]
        private ObservableCollection<BoxExportItem> availableBoxes = new ObservableCollection<BoxExportItem>();

        [ObservableProperty]
        private int selectedCount;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private bool hasSelection;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = "";

        public ExportSelectViewModel(ExportImportService exportImportService)
        {
            _exportImportService = exportImportService;
        }

        public async Task LoadBoxesAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Loading boxes...";

                var boxes = await _exportImportService.GetAvailableBoxesForExport();

                AvailableBoxes.Clear();
                foreach (var box in boxes)
                {
                    // Subscribe to property changes for selection tracking
                    box.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(BoxExportItem.IsSelected))
                        {
                            UpdateSelectionCount();
                        }
                    };
                    AvailableBoxes.Add(box);
                }

                TotalCount = AvailableBoxes.Count;
                UpdateSelectionCount();
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to load boxes: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                StatusMessage = "";
            }
        }

        [RelayCommand]
        private void ToggleSelection(BoxExportItem? item)
        {
            if (item != null)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            // If all are selected, deselect all. Otherwise, select all.
            bool shouldSelectAll = AvailableBoxes.Any(b => !b.IsSelected);

            foreach (var box in AvailableBoxes)
            {
                box.IsSelected = shouldSelectAll;
            }

            UpdateSelectionCount();
        }

        [RelayCommand]
        private async Task Export()
        {
            var selectedBoxes = AvailableBoxes.Where(b => b.IsSelected).ToList();

            if (!selectedBoxes.Any())
            {
                await ShowAlert("No Selection", "Please select at least one box to export.");
                return;
            }

            try
            {
                // Check for storage permissions on Android
#if ANDROID
                var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    if (status != PermissionStatus.Granted)
                    {
                        await ShowAlert("Permission Required", "Storage permission is required to export data.");
                        return;
                    }
                }
#endif

                IsBusy = true;
                StatusMessage = $"Exporting {selectedBoxes.Count} box(es)...";

                var result = await _exportImportService.ExportSelectedBoxes(selectedBoxes);

                if (result.Success)
                {
                    var message = $"Successfully exported {result.BoxesExported} box(es):\n\n";
                    foreach (var box in selectedBoxes)
                    {
                        message += $"✓ {box.Summary}\n";
                    }
                    message += $"\nSaved to: {result.ExportPath}";

                    await ShowAlert("Export Complete", message);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await ShowAlert("Export Failed", result.ErrorMessage ?? "Unknown error occurred");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Export failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                StatusMessage = "";
            }
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }

        private void UpdateSelectionCount()
        {
            SelectedCount = AvailableBoxes.Count(b => b.IsSelected);
            HasSelection = SelectedCount > 0;
        }

        private async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "OK");
            }
        }
    }
}