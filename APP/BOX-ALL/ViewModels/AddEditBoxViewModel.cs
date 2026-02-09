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
    public partial class AddEditBoxViewModel : ObservableObject
    {
        private readonly BoxRegistryService _boxRegistryService;

        [ObservableProperty]
        private string pageTitle = "Create Storage Box";

        [ObservableProperty]
        private string boxName = "";

        [ObservableProperty]
        private List<BoxTypeOption> boxTypes = new();

        [ObservableProperty]
        private BoxTypeOption? selectedBoxType;

        [ObservableProperty]
        private bool isAntiStatic = false;

        [ObservableProperty]
        private bool antiStaticEnabled = true;

        [ObservableProperty]
        private string saveButtonText = "Create";

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private bool isBusy = false;

        public AddEditBoxViewModel(BoxRegistryService boxRegistryService)
        {
            _boxRegistryService = boxRegistryService;
        }

        public async Task InitializeAsync()
        {
            // Initialize box types (sorted by compartments, high to low)
            BoxTypes = new List<BoxTypeOption>
            {
                new BoxTypeOption("144", 144, 12, 12, enabled: true, supportsAS: true),
                new BoxTypeOption("96", 96, 10, 12, enabled: true, supportsAS: true),
                new BoxTypeOption("48", 48, 4, 12, enabled: false, supportsAS: false),
                new BoxTypeOption("40", 40, 4, 10, enabled: false, supportsAS: false),
                new BoxTypeOption("24", 24, 2, 12, enabled: false, supportsAS: false)
            };

            // Select first enabled type by default
            SelectBoxType(BoxTypes.First(t => t.IsEnabled));
        }

        [RelayCommand]
        private void SelectBoxType(BoxTypeOption? option)
        {
            if (option == null || !option.IsEnabled) return;

            // Deselect all
            foreach (var bt in BoxTypes)
            {
                bt.IsSelected = false;
            }

            // Select the tapped one
            option.IsSelected = true;
            SelectedBoxType = option;

            // Enable/disable AS checkbox based on box type
            AntiStaticEnabled = option.SupportsAntiStatic;

            // Auto-uncheck AS if not supported
            if (!AntiStaticEnabled)
            {
                IsAntiStatic = false;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(BoxName))
            {
                await ShowAlert("Validation Error", "Please enter a box name.");
                return;
            }

            if (SelectedBoxType == null)
            {
                await ShowAlert("Validation Error", "Please select a box type.");
                return;
            }

            try
            {
                IsBusy = true;

                // Build the full type code
                string fullTypeCode = BuildTypeCode(SelectedBoxType.TypeCode, IsAntiStatic);

                // Create the box
                var newBox = await _boxRegistryService.CreateBoxAsync(BoxName, fullTypeCode);

                if (newBox != null)
                {
                    await ShowAlert("Success", $"Box '{BoxName}' created successfully!");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await ShowAlert("Error", "Failed to create box.");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to create box: {ex.Message}");
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

        private string BuildTypeCode(string baseType, bool antiStatic)
        {
            // Map base type to full type code
            string typeCode = baseType switch
            {
                "144" => antiStatic ? "BOXALL144AS" : "BOXALL144",
                "96" => antiStatic ? "BOXALL96AS" : "BOXALL96",
                "48" => "BOXALL48",
                "40" => "BOXALL40",
                "24" => "BOXALL24",
                _ => "BOXALL144AS"
            };

            return typeCode;
        }

        private async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }
    }
}
