using BOX_ALL.Models;
using BOX_ALL.Services;
using BOX_ALL.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Component = BOX_ALL.Models.Component;

namespace BOX_ALL.ViewModels
{
    [QueryProperty(nameof(PreselectedPosition), "position")]
    [QueryProperty(nameof(EditMode), "editMode")]
    [QueryProperty(nameof(ComponentId), "componentId")]
    public partial class AddComponentViewModel : ObservableObject
    {
        private readonly BoxDataService _boxDataService;
        private readonly BoxRegistryService _boxRegistryService;

        [ObservableProperty]
        private Component component = new Component();

        [ObservableProperty]
        private string selectedPosition = "";

        [ObservableProperty]
        private ObservableCollection<string> categories = new ObservableCollection<string>();

        [ObservableProperty]
        private ObservableCollection<string> availablePositions = new ObservableCollection<string>();

        [ObservableProperty]
        private string selectedCategory = "Other";

        [ObservableProperty]
        private string supplier = "";

        [ObservableProperty]
        private string quantity = "0";

        [ObservableProperty]
        private string minQuantity = "10";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string pageTitle = "Add Component";

        [ObservableProperty]
        private bool editMode = false;

        [ObservableProperty]
        private string existingComponentWarning = "";

        [ObservableProperty]
        private bool showExistingWarning = false;

        public string? PreselectedPosition { get; set; }
        public string? ComponentId { get; set; }

        /// <summary>
        /// The box ID to operate on. Set before calling InitializeAsync().
        /// </summary>
        public string? RequestedBoxId { get; set; }

        private string? _currentBoxId;
        private BoxData? _currentBoxData;
        private string? _editingPosition;

        // Properties used by AddComponentPage XAML bindings
        public string SupplierPartNumber
        {
            get => Component?.SupplierPartNumber ?? "";
            set { if (Component != null) Component.SupplierPartNumber = value; }
        }

        public string Value
        {
            get => Component?.Value ?? "";
            set { if (Component != null) Component.Value = value; }
        }

        public string Package
        {
            get => Component?.Package ?? "";
            set { if (Component != null) Component.Package = value; }
        }

        public string Tolerance
        {
            get => Component?.Tolerance ?? "";
            set { if (Component != null) Component.Tolerance = value; }
        }

        public string Voltage
        {
            get => Component?.Voltage ?? "";
            set { if (Component != null) Component.Voltage = value; }
        }

        public decimal UnitPrice
        {
            get => Component?.UnitPrice ?? 0;
            set { if (Component != null) Component.UnitPrice = value; }
        }

        public string Notes
        {
            get => Component?.Notes ?? "";
            set { if (Component != null) Component.Notes = value; }
        }

        public string DatasheetUrl
        {
            get => Component?.DatasheetUrl ?? "";
            set { if (Component != null) Component.DatasheetUrl = value; }
        }

        public string Manufacturer
        {
            get => Component?.Manufacturer ?? "";
            set { if (Component != null) Component.Manufacturer = value; }
        }

        public AddComponentViewModel(BoxDataService boxDataService, BoxRegistryService boxRegistryService)
        {
            _boxDataService = boxDataService;
            _boxRegistryService = boxRegistryService;
            Component = new Component();
            LoadCategories();
            InitializeDefaults();
        }

        private void LoadCategories()
        {
            Categories = new ObservableCollection<string>
            {
                "Resistor",
                "Capacitor",
                "Inductor",
                "Diode",
                "LED",
                "Transistor",
                "MOSFET",
                "IC",
                "Microcontroller",
                "Crystal",
                "Connector",
                "Switch",
                "Button",
                "Relay",
                "Fuse",
                "Voltage Regulator",
                "Op-Amp",
                "Sensor",
                "Display",
                "Other"
            };
            SelectedCategory = "Other";
        }

        private async Task LoadAvailablePositions()
        {
            AvailablePositions.Clear();

            // Determine box type from the loaded box
            string boxType = "BOXALL144"; // default
            if (!string.IsNullOrEmpty(RequestedBoxId))
            {
                var boxes = await _boxRegistryService.GetAllBoxesAsync();
                var box = boxes.FirstOrDefault(b => b.Id == RequestedBoxId);
                if (box != null)
                {
                    boxType = box.Type;
                }
            }

            // Generate positions using the shared helper
            var positions = PositionHelper.GetAllPositions(boxType);
            foreach (var pos in positions)
            {
                AvailablePositions.Add(pos);
            }
        }

        private void InitializeDefaults()
        {
            MinQuantity = "10";
            Quantity = "0";
        }

        public async Task InitializeAsync(string? position = null)
        {
            await LoadAvailablePositions();

            // Get the correct box using RequestedBoxId, fall back to first box
            var boxes = await _boxRegistryService.GetAllBoxesAsync();
            if (boxes.Any())
            {
                BoxRegistryItem? targetBox = null;
                if (!string.IsNullOrEmpty(RequestedBoxId))
                {
                    targetBox = boxes.FirstOrDefault(b => b.Id == RequestedBoxId);
                }
                targetBox ??= boxes.First();

                _currentBoxId = targetBox.Id;
                _currentBoxData = await _boxDataService.LoadBoxAsync(_currentBoxId);
            }

            // Check if we're in edit mode
            if (!string.IsNullOrEmpty(ComponentId) && !string.IsNullOrEmpty(position))
            {
                EditMode = true;
                await LoadComponentForEdit(position);
                return;
            }

            if (!string.IsNullOrEmpty(position))
            {
                SelectedPosition = position;
            }
            else if (!string.IsNullOrEmpty(PreselectedPosition))
            {
                SelectedPosition = PreselectedPosition;
            }
        }

        private async Task LoadComponentForEdit(string position)
        {
            try
            {
                PageTitle = "Edit Component";
                _editingPosition = position;

                if (_currentBoxData != null)
                {
                    var compartment = _currentBoxData.GetCompartment(position);
                    if (compartment?.Component != null)
                    {
                        Component = Component.FromComponentData(compartment.Component);
                        SelectedCategory = Component.Category ?? "Other";
                        Supplier = Component.Supplier ?? "";
                        SelectedPosition = position;
                        Quantity = compartment.Component.Quantity.ToString();
                        MinQuantity = compartment.Component.MinStock.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("Error",
                        $"Failed to load component: {ex.Message}", "OK");
                }
            }
        }

        partial void OnSelectedPositionChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = CheckForExistingComponent(value);
            }
        }

        private async Task CheckForExistingComponent(string position)
        {
            if (_currentBoxData == null) return;

            try
            {
                var compartment = _currentBoxData.GetCompartment(position);
                if (compartment?.Component != null && !string.IsNullOrEmpty(compartment.Component.PartNumber))
                {
                    // Don't warn in edit mode if it's the same position we're editing
                    if (EditMode && position == _editingPosition) return;

                    ExistingComponentWarning = $"⚠️ Position {position} has: {compartment.Component.PartNumber}";
                    ShowExistingWarning = true;
                }
                else
                {
                    ExistingComponentWarning = "";
                    ShowExistingWarning = false;
                }
            }
            catch
            {
                ExistingComponentWarning = "";
                ShowExistingWarning = false;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(SelectedPosition))
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("Error",
                        "Please select a position", "OK");
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(Component.PartNumber))
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("Error",
                        "Please enter a part number", "OK");
                }
                return;
            }

            IsBusy = true;

            try
            {
                Component.Category = SelectedCategory;
                Component.Supplier = Supplier;

                var componentData = Component.ToComponentData(
                    int.TryParse(Quantity, out int qty) ? qty : 0,
                    int.TryParse(MinQuantity, out int min) ? min : 10
                );

                // Save to the selected position
                await _boxDataService.AddComponentAsync(_currentBoxId, SelectedPosition.ToUpper(), componentData);

                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("Success",
                        EditMode ? "Component updated successfully" : "Component saved successfully", "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert("Error",
                        $"Failed to save component: {ex.Message}", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
