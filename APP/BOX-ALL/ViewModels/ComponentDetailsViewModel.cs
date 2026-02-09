using BOX_ALL.Models;
using BOX_ALL.Services;
using BOX_ALL.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Component = BOX_ALL.Models.Component;
using BOX_ALL.Helpers;

namespace BOX_ALL.ViewModels
{
    public partial class ComponentDetailsViewModel : ObservableObject
    {
        private readonly BoxDataService _boxDataService;
        private readonly BoxRegistryService _boxRegistryService;
        private readonly ImportLogService _importLogService;

        [ObservableProperty]
        private Component? component;

        [ObservableProperty]
        private Models.Location? location;

        [ObservableProperty]
        private string supplier = "";

        [ObservableProperty]
        private Color stockStatusColor = Color.FromArgb("#4ADE80");

        [ObservableProperty]
        private string adjustmentQuantity = "1";

        [ObservableProperty]
        private bool isBusy;

        // Visibility properties for optional fields
        [ObservableProperty]
        private bool hasSupplierInfo;

        [ObservableProperty]
        private bool hasSupplierPartNumber;

        [ObservableProperty]
        private bool hasTechnicalSpecs;

        [ObservableProperty]
        private bool hasValue;

        [ObservableProperty]
        private bool hasPackage;

        [ObservableProperty]
        private bool hasTolerance;

        [ObservableProperty]
        private bool hasVoltage;

        [ObservableProperty]
        private bool hasNotes;

        private string? _currentBoxId;
        private string? _currentPosition;
        private BoxData? _currentBoxData;

        public ComponentDetailsViewModel(
            BoxDataService boxDataService,
            BoxRegistryService boxRegistryService,
            ImportLogService importLogService)
        {
            _boxDataService = boxDataService;
            _boxRegistryService = boxRegistryService;
            _importLogService = importLogService;
        }

        public async Task LoadComponentAsync(Guid componentId, string? position, string? boxId = null)
        {
            try
            {
                IsBusy = true;

                if (string.IsNullOrEmpty(position))
                {
                    await ShowAlert("Error", "Position not specified");
                    return;
                }

                _currentPosition = position;

                // Get the correct box using boxId, fall back to first box
                var boxes = await _boxRegistryService.GetAllBoxesAsync();
                if (boxes.Any())
                {
                    BoxRegistryItem? targetBox = null;
                    if (!string.IsNullOrEmpty(boxId))
                    {
                        targetBox = boxes.FirstOrDefault(b => b.Id == boxId);
                    }
                    targetBox ??= boxes.First();

                    _currentBoxId = targetBox.Id;
                    _currentBoxData = await _boxDataService.LoadBoxAsync(_currentBoxId);

                    if (_currentBoxData != null)
                    {
                        var compartment = _currentBoxData.GetCompartment(position);
                        if (compartment?.Component != null)
                        {
                            Component = Component.FromComponentData(compartment.Component);

                            // Create a Location object for compatibility
                            Location = new Models.Location
                            {
                                Position = position,
                                Quantity = compartment.Component.Quantity,
                                MinQuantity = compartment.Component.MinStock,
                                LastUpdated = compartment.Component.LastUpdated,
                                Component = Component
                            };

                            AdjustmentQuantity = Location.Quantity.ToString();
                            UpdateStockStatusColor();
                            UpdateVisibilityFlags();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to load component: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateStockStatusColor()
        {
            if (Location == null) return;

            if (Location.Quantity == 0)
            {
                StockStatusColor = Color.FromArgb("#FF6B6B"); // Red for out of stock
            }
            else if (Location.IsLowStock)
            {
                StockStatusColor = Color.FromArgb("#FBBF24"); // Amber for low stock
            }
            else
            {
                StockStatusColor = Color.FromArgb("#4ADE80"); // Green for good stock
            }
        }

        private void UpdateVisibilityFlags()
        {
            if (Component == null) return;

            Supplier = Component.Supplier ?? "";

            HasSupplierInfo = !string.IsNullOrWhiteSpace(Supplier);
            HasSupplierPartNumber = !string.IsNullOrWhiteSpace(Component.SupplierPartNumber);
            HasValue = !string.IsNullOrWhiteSpace(Component.Value);
            HasPackage = !string.IsNullOrWhiteSpace(Component.Package);
            HasTolerance = !string.IsNullOrWhiteSpace(Component.Tolerance);
            HasVoltage = !string.IsNullOrWhiteSpace(Component.Voltage);
            HasNotes = !string.IsNullOrWhiteSpace(Component.Notes);

            HasTechnicalSpecs = HasValue || HasPackage || HasTolerance || HasVoltage;
        }

        [RelayCommand]
        private async Task Edit()
        {
            if (Component == null || string.IsNullOrEmpty(_currentPosition)) return;

            var parameters = new Dictionary<string, object>
            {
                { "componentId", Component.Id.ToString() },
                { "position", _currentPosition },
                { "editMode", "true" },
                { "boxId", _currentBoxId ?? "" }
            };
            await Shell.Current.GoToAsync($"{nameof(AddComponentPage)}", parameters);
        }

        [RelayCommand]
        private async Task Relocate()
        {
            if (Component == null || Location == null || _currentBoxId == null || _currentBoxData == null) return;

            try
            {
                IsBusy = true;

                string oldPosition = Location.Position;

                // Generate all positions for the grid based on box type
                string boxType = "BOXALL144"; // default
                if (!string.IsNullOrEmpty(_currentBoxId))
                {
                    var boxItem = await _boxRegistryService.GetBoxAsync(_currentBoxId);
                    if (boxItem != null) boxType = boxItem.Type;
                }
                var allPositions = PositionHelper.GetAllPositionsExcept(boxType, oldPosition);

                // Show position picker
                string newPosition = "";
                if (Application.Current?.Windows?.Count > 0)
                {
                    newPosition = await Application.Current.Windows[0].Page!.DisplayActionSheet(
                        $"Relocate from {oldPosition} to:", "Cancel", null, allPositions.ToArray());
                }

                if (string.IsNullOrEmpty(newPosition) || newPosition == "Cancel")
                {
                    return;
                }

                // Validate the new position format
                if (!PositionHelper.IsValidPosition(boxType, newPosition))
                {
                    await ShowAlert("Invalid Position", $"'{newPosition}' is not a valid position format");
                    return;
                }

                // Check if new position is occupied
                var existingCompartment = _currentBoxData.GetCompartment(newPosition);
                if (existingCompartment?.Component != null)
                {
                    var existingComponent = existingCompartment.Component;
                    string existingInfo = $"{existingComponent.PartNumber} ({existingComponent.Quantity} units)";

                    bool proceed = await ShowConfirm("Position Occupied",
                        $"Position {newPosition} contains:\n{existingInfo}\n\nReplace with current component?");

                    if (!proceed)
                    {
                        return;
                    }
                }

                // Move the component
                await _boxDataService.MoveComponentAsync(_currentBoxId, oldPosition, newPosition);

                // Update import log: move entry from old position to new, remove any entry at destination
                await _importLogService.UpdatePositionAsync(_currentBoxId, oldPosition, newPosition);

                await ShowAlert("Success", $"Component relocated from {oldPosition} to {newPosition}");

                // Navigate back to box view
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to relocate component: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Delete()
        {
            if (Component == null || _currentBoxId == null || string.IsNullOrEmpty(_currentPosition)) return;

            bool confirm = await ShowConfirm("Delete Component",
                $"Are you sure you want to delete {Component.PartNumber}?");

            if (confirm)
            {
                try
                {
                    IsBusy = true;

                    await _boxDataService.DeleteComponentAsync(_currentBoxId, _currentPosition);

                    // Release this position from import log so it can be re-imported
                    await _importLogService.RemoveEntryAsync(_currentBoxId, _currentPosition);

                    await ShowAlert("Success", "Component deleted successfully");
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    await ShowAlert("Error", $"Failed to delete component: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void IncrementQuantity()
        {
            if (int.TryParse(AdjustmentQuantity, out int qty))
            {
                AdjustmentQuantity = (qty + 1).ToString();
            }
            else
            {
                AdjustmentQuantity = Location != null ? (Location.Quantity + 1).ToString() : "1";
            }
        }

        [RelayCommand]
        private void DecrementQuantity()
        {
            if (int.TryParse(AdjustmentQuantity, out int qty) && qty > 0)
            {
                AdjustmentQuantity = (qty - 1).ToString();
            }
            else if (Location != null && Location.Quantity > 0)
            {
                AdjustmentQuantity = (Location.Quantity - 1).ToString();
            }
        }

        [RelayCommand]
        private async Task UpdateQuantity()
        {
            if (Location == null || _currentBoxId == null || _currentBoxData == null || string.IsNullOrEmpty(_currentPosition)) return;

            if (!int.TryParse(AdjustmentQuantity, out int adjustment))
            {
                await ShowAlert("Invalid Input", "Please enter a valid number");
                return;
            }

            if (adjustment < 0)
            {
                await ShowAlert("Invalid Input", "Quantity cannot be negative");
                return;
            }

            try
            {
                IsBusy = true;

                var compartment = _currentBoxData.GetCompartment(_currentPosition);
                if (compartment?.Component != null)
                {
                    compartment.Component.Quantity = adjustment;
                    compartment.Component.LastUpdated = DateTime.Now;
                    await _boxDataService.SaveBoxAsync(_currentBoxData);

                    Location.Quantity = adjustment;
                    OnPropertyChanged(nameof(Location));
                    UpdateStockStatusColor();

                    await ShowAlert("Success", $"Quantity updated to {adjustment}");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to update quantity: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Helper methods for alerts
        private async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "OK");
            }
        }

        private async Task<bool> ShowConfirm(string title, string message)
        {
            if (Application.Current?.Windows?.Count > 0)
            {
                return await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Yes", "No");
            }
            return false;
        }
    }
}
