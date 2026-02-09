using BOX_ALL.Services;
using BOX_ALL.ViewModels;
using BOX_ALL.Views;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace BOX_ALL
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute(nameof(BoxViewPage), typeof(BoxViewPage));
            Routing.RegisterRoute(nameof(AddComponentPage), typeof(AddComponentPage));
            Routing.RegisterRoute(nameof(BulkImportPage), typeof(BulkImportPage));
            Routing.RegisterRoute(nameof(ComponentDetailsPage), typeof(ComponentDetailsPage));
            Routing.RegisterRoute(nameof(ExportSelectPage), typeof(ExportSelectPage));
            Routing.RegisterRoute(nameof(ImportSelectPage), typeof(ImportSelectPage));
            Routing.RegisterRoute(nameof(AddEditBoxPage), typeof(AddEditBoxPage));

            BindingContext = this;
        }

        /// <summary>
        /// Resolve a registered service from the DI container.
        /// </summary>
        private T GetService<T>() where T : notnull
        {
            return Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<T>();
        }

        // ── Box Management ──────────────────────────────────────────

        [RelayCommand]
        private async Task CreateBox()
        {
            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(AddEditBoxPage));
        }

        [RelayCommand]
        private async Task DeleteBox()
        {
            Shell.Current.FlyoutIsPresented = false;

            // Delegate to DashboardViewModel if we're on the Dashboard with a box selected
            if (Shell.Current.CurrentPage?.BindingContext is DashboardViewModel vm && vm.HasSelectedBox)
            {
                await vm.DeleteBoxCommand.ExecuteAsync(null);
            }
            else
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert(
                        "Delete Box",
                        "Go to the Dashboard and select a box first.",
                        "OK");
                }
            }
        }

        // ── Import & Sync ───────────────────────────────────────────

        [RelayCommand]
        private async Task ImportParts()
        {
            Shell.Current.FlyoutIsPresented = false;

            try
            {
                // Check if any boxes exist
                var boxRegistry = GetService<BoxRegistryService>();
                var boxes = await boxRegistry.GetAllBoxesAsync();

                if (boxes.Count == 0)
                {
                    if (Application.Current?.Windows?.Count > 0)
                    {
                        await Application.Current.Windows[0].Page!.DisplayAlert(
                            "No Storage Box",
                            "Please create a storage box first before importing components.",
                            "OK");
                    }
                    return;
                }

                // Open file picker for CSV
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select BOX-ALL Import CSV",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values", "application/csv" } },
                        { DevicePlatform.WinUI, new[] { ".csv" } }
                    })
                });

                if (result != null)
                {
                    await Shell.Current.GoToAsync($"{nameof(BulkImportPage)}?csvFile={result.FullPath}");
                }
            }
            catch (System.Exception ex)
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert(
                        "Error", $"Failed to pick file: {ex.Message}", "OK");
                }
            }
        }

        [RelayCommand]
        private async Task Synchronize()
        {
            Shell.Current.FlyoutIsPresented = false;

            try
            {
                var syncService = GetService<SyncExportService>();
                var filePath = await syncService.ExportStatusAsync();

                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert(
                        "Sync Complete",
                        "Status exported to:\nDocuments/BOX-ALL/exports/boxall_status.json",
                        "OK");
                }
            }
            catch (System.Exception ex)
            {
                if (Application.Current?.Windows?.Count > 0)
                {
                    await Application.Current.Windows[0].Page!.DisplayAlert(
                        "Sync Failed", $"Error: {ex.Message}", "OK");
                }
            }
        }

        // ── Data Management ─────────────────────────────────────────

        [RelayCommand]
        private async Task Export()
        {
            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(ExportSelectPage));
        }

        [RelayCommand]
        private async Task Import()
        {
            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(nameof(ImportSelectPage));
        }

        // ── App Info ────────────────────────────────────────────────

        [RelayCommand]
        private async Task Settings()
        {
            Shell.Current.FlyoutIsPresented = false;

            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert("Settings",
                    "Settings page coming soon!", "OK");
            }
        }

        [RelayCommand]
        private async Task About()
        {
            Shell.Current.FlyoutIsPresented = false;

            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert("About",
                    "BOX-ALL v1.0\nComponent Inventory Manager\n© 2024 INTECTOR", "OK");
            }
        }
    }
}
