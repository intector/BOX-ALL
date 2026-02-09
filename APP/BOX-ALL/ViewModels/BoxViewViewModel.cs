using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Models;
using BOX_ALL.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BOX_ALL.ViewModels
{
    public partial class BoxViewViewModel : ObservableObject
    {
        private readonly BoxDataService _boxDataService;
        private readonly BoxRegistryService _boxRegistryService;
        private readonly ImportLogService _importLogService;

        [ObservableProperty]
        private Box? currentBox;

        [ObservableProperty]
        private int occupiedCount;

        [ObservableProperty]
        private int totalCompartments;

        [ObservableProperty]
        private List<Models.Location> locations = new List<Models.Location>();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string title = "Box View";

        [ObservableProperty]
        private bool isEditingName;

        [ObservableProperty]
        private string editBoxName = "";

        private BoxData? _currentBoxData;

        /// <summary>
        /// The requested box ID passed via navigation query parameter.
        /// Set this before calling LoadDataAsync().
        /// </summary>
        public string? RequestedBoxId { get; set; }

        /// <summary>
        /// The currently loaded box ID (available after LoadDataAsync).
        /// </summary>
        public string? CurrentBoxId { get; private set; }

        public BoxViewViewModel(
            BoxDataService boxDataService,
            BoxRegistryService boxRegistryService,
            ImportLogService importLogService)
        {
            _boxDataService = boxDataService;
            _boxRegistryService = boxRegistryService;
            _importLogService = importLogService;
            Locations = new List<Models.Location>();
        }

        public async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;

                var boxes = await _boxRegistryService.GetAllBoxesAsync();
                if (boxes.Any())
                {
                    // Use the requested boxId if provided, otherwise fall back to first box
                    BoxRegistryItem? boxItem = null;

                    if (!string.IsNullOrEmpty(RequestedBoxId) && RequestedBoxId != "none")
                    {
                        boxItem = boxes.FirstOrDefault(b => b.Id == RequestedBoxId);
                    }

                    // Fall back to first box if requested box not found
                    boxItem ??= boxes.First();

                    CurrentBoxId = boxItem.Id;
                    CurrentBox = Box.FromBoxRegistryItem(boxItem);
                    TotalCompartments = boxItem.TotalCompartments;

                    // Load box data
                    _currentBoxData = await _boxDataService.LoadBoxAsync(boxItem.Id);

                    if (_currentBoxData != null)
                    {
                        // Build location list from compartments
                        var locationList = new List<Models.Location>();
                        foreach (var compartment in _currentBoxData.Compartments)
                        {
                            if (compartment.Component != null)
                            {
                                var comp = compartment.Component;
                                var location = new Models.Location
                                {
                                    Position = compartment.Position,
                                    ComponentId = Guid.NewGuid(),
                                    Quantity = comp.Quantity,
                                    MinQuantity = comp.MinStock,
                                    LastUpdated = comp.LastUpdated,
                                    Component = Component.FromComponentData(comp)
                                };
                                locationList.Add(location);
                            }
                        }

                        Locations = locationList;
                        OccupiedCount = locationList.Count;

                        // Update stats in registry
                        int lowStockCount = locationList.Count(l => l.Quantity > 0 && l.IsLowStock);
                        await _boxRegistryService.UpdateBoxStatsAsync(boxItem.Id, OccupiedCount, lowStockCount);
                    }
                    else
                    {
                        Locations = new List<Models.Location>();
                        OccupiedCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading box data: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Enter inline edit mode for the box name.
        /// </summary>
        public void StartEditing()
        {
            EditBoxName = CurrentBox?.Name ?? "";
            IsEditingName = true;
        }

        /// <summary>
        /// Commit the rename or cancel if unchanged/empty.
        /// </summary>
        [RelayCommand]
        private async Task RenameBox()
        {
            if (string.IsNullOrWhiteSpace(EditBoxName) || CurrentBoxId == null)
            {
                IsEditingName = false;
                return;
            }

            var trimmed = EditBoxName.Trim();
            if (trimmed == CurrentBox?.Name)
            {
                IsEditingName = false;
                return;
            }

            var success = await _boxRegistryService.RenameBoxAsync(CurrentBoxId, trimmed);
            if (success && CurrentBox != null)
            {
                CurrentBox.Name = trimmed;
                OnPropertyChanged(nameof(CurrentBox));

                // Keep import log in sync with new box name
                await _importLogService.UpdateBoxNameAsync(CurrentBoxId, trimmed);
            }

            IsEditingName = false;
        }
    }
}
