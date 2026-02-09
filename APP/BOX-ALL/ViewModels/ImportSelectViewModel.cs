using BOX_ALL.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BOX_ALL.ViewModels
{
    // Wrapper class to make ImportFileInfo observable
    public partial class ImportFileViewModel : ObservableObject
    {
        private readonly ImportFileInfo _fileInfo;

        [ObservableProperty]
        private bool isSelected;

        public ImportFileViewModel(ImportFileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            isSelected = fileInfo.IsSelected;
        }

        // Expose properties from ImportFileInfo
        public string FilePath => _fileInfo.FilePath;
        public string FileName => _fileInfo.FileName;
        public string BoxId => _fileInfo.BoxId;
        public string BoxName => _fileInfo.BoxName;
        public string BoxType => _fileInfo.BoxType;
        public int ComponentCount => _fileInfo.ComponentCount;
        public DateTime ExportDate => _fileInfo.ExportDate;
        public bool IsExistingBox => _fileInfo.IsExistingBox;
        public bool IsNewBox => !_fileInfo.IsExistingBox;  // For XAML binding

        partial void OnIsSelectedChanged(bool value)
        {
            _fileInfo.IsSelected = value;
        }
    }

    public partial class ImportSelectViewModel : ObservableObject
    {
        private readonly ExportImportService _exportImportService;

        [ObservableProperty]
        private ObservableCollection<ImportFileViewModel> availableFiles = new ObservableCollection<ImportFileViewModel>();

        [ObservableProperty]
        private int selectedCount;

        [ObservableProperty]
        private int overwriteCount;

        [ObservableProperty]
        private int newBoxCount;

        [ObservableProperty]
        private bool hasSelection;

        [ObservableProperty]
        private bool hasOverwrites;

        [ObservableProperty]
        private bool hasNewBoxes;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = "";

        [ObservableProperty]
        private bool showEmptyState;

        [ObservableProperty]
        private bool requiresRecovery;

        [ObservableProperty]
        private string exportFolderPath = "";

        public ImportSelectViewModel(ExportImportService exportImportService)
        {
            _exportImportService = exportImportService;
        }

        public async Task LoadFilesAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Checking for import files...";
                AvailableFiles.Clear();

                // Check import access and detect recovery scenario
                var accessResult = await _exportImportService.CheckImportAccess();
                ExportFolderPath = accessResult.ExportFolderPath;

                if (accessResult.HasDirectAccess && accessResult.Files.Any())
                {
                    // Normal case - we have access and files
                    RequiresRecovery = false;
                    await LoadFileList(accessResult.Files);
                }
                else if (accessResult.RequiresRecovery)
                {
                    // Reinstall scenario - need to recover access
                    RequiresRecovery = true;
                    ShowEmptyState = false;
                    StatusMessage = "";

                    // Show recovery dialog
                    await ShowRecoveryDialog();
                }
                else
                {
                    // No files found
                    RequiresRecovery = false;
                    ShowEmptyState = true;
                    StatusMessage = "";
                }

                UpdateSelectionCount();
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to load import files: {ex.Message}");
                ShowEmptyState = true;
            }
            finally
            {
                IsBusy = false;
                if (!RequiresRecovery)
                {
                    StatusMessage = "";
                }
            }
        }

        private async Task ShowRecoveryDialog()
        {
            var message = "It looks like BOX-ALL was reinstalled. Your previous export files are still on your device but need to be reconnected.\n\n" +
                         $"Your exports should be in:\n📁 {ExportFolderPath}\n\n" +
                         "Tap 'Locate Folder' to select this folder and restore access to your exported boxes.";

            bool result = await Application.Current!.MainPage!.DisplayAlert(
                "Recover Previous Exports",
                message,
                "Locate Folder",
                "Cancel"
            );

            if (result)
            {
                await LaunchFolderPicker();
            }
            else
            {
                ShowEmptyState = true;
                RequiresRecovery = false;
            }
        }

        private async Task LaunchFolderPicker()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Opening folder selector...";

#if ANDROID
                // Launch platform-specific folder picker
                var folderPickerService = Application.Current!.MainPage!.Handler?.MauiContext?.Services.GetService<IFolderPickerService>();
                if (folderPickerService != null)
                {
                    var selectedUri = await folderPickerService.PickExportFolderAsync();
                    if (!string.IsNullOrEmpty(selectedUri))
                    {
                        // Process the selected folder
                        await ProcessSelectedFolder(selectedUri);
                    }
                    else
                    {
                        // User cancelled
                        ShowEmptyState = true;
                        RequiresRecovery = false;
                    }
                }
                else
                {
                    // Fallback - for now just try to get files again
                    // In production, this would use dependency injection properly
                    await FallbackFileAccess();
                }
#else
                // Non-Android platforms - try direct access
                await FallbackFileAccess();
#endif
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to select folder: {ex.Message}");
                ShowEmptyState = true;
                RequiresRecovery = false;
            }
            finally
            {
                IsBusy = false;
                StatusMessage = "";
            }
        }

        private async Task ProcessSelectedFolder(string uriString)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Reading export files...";

#if ANDROID
                // Use SAF to get files from the selected folder
                var files = await _exportImportService.GetFilesFromSAFUri(uriString);
                
                if (files.Any())
                {
                    RequiresRecovery = false;
                    
                    // Show success message
                    await ShowAlert(
                        "Success", 
                        $"✓ Export folder connected successfully!\nFound {files.Count} export file(s)."
                    );
                    
                    // Load the files into the list
                    await LoadFileList(files);
                }
                else
                {
                    // Wrong folder or no files
                    bool retry = await Application.Current!.MainPage!.DisplayAlert(
                        "No Export Files Found",
                        "The selected folder doesn't contain any BOX-ALL export files.\n\n" +
                        "Please make sure you select the 'exports' folder inside 'Documents/BOX-ALL/'.",
                        "Try Again",
                        "Cancel"
                    );

                    if (retry)
                    {
                        await LaunchFolderPicker();
                    }
                    else
                    {
                        ShowEmptyState = true;
                        RequiresRecovery = false;
                    }
                }
#else
                await FallbackFileAccess();
#endif
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Failed to read files from selected folder: {ex.Message}");
                ShowEmptyState = true;
                RequiresRecovery = false;
            }
        }

        private async Task FallbackFileAccess()
        {
            // Try direct file access as fallback
            var files = await _exportImportService.GetAvailableImportFiles();
            if (files.Any())
            {
                RequiresRecovery = false;
                await LoadFileList(files);
            }
            else
            {
                ShowEmptyState = true;
                RequiresRecovery = false;
            }
        }

        private async Task LoadFileList(List<ImportFileInfo> files)
        {
            AvailableFiles.Clear();

            foreach (var file in files)
            {
                var fileViewModel = new ImportFileViewModel(file);

                // Subscribe to property changes for selection tracking
                fileViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ImportFileViewModel.IsSelected))
                    {
                        UpdateSelectionCount();
                    }
                };

                AvailableFiles.Add(fileViewModel);
            }

            ShowEmptyState = !AvailableFiles.Any();
            UpdateSelectionCount();
        }

        [RelayCommand]
        private void ToggleSelection(ImportFileViewModel? item)
        {
            if (item != null)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        [RelayCommand]
        private async Task RecoverFolder()
        {
            // Manual recovery trigger (if shown in UI)
            await LaunchFolderPicker();
        }

        [RelayCommand]
        private async Task Import()
        {
            var selectedFiles = AvailableFiles.Where(f => f.IsSelected).ToList();

            if (!selectedFiles.Any())
            {
                await ShowAlert("No Selection", "Please select at least one file to import.");
                return;
            }

            try
            {
                // Build overwrite decisions dictionary
                var overwriteDecisions = new Dictionary<string, bool>();
                var existingBoxFiles = selectedFiles.Where(f => f.IsExistingBox).ToList();

                // If there are existing boxes, confirm overwrites
                if (existingBoxFiles.Any())
                {
                    var message = "The following boxes already exist and will be overwritten:\n\n";
                    foreach (var file in existingBoxFiles)
                    {
                        message += $"• {file.BoxName}\n";
                    }
                    message += "\nContinue with import?";

                    bool proceed = await ShowConfirm("Overwrite Existing Boxes?", message);

                    if (!proceed)
                    {
                        return;
                    }

                    // Mark all existing boxes for overwrite
                    foreach (var file in existingBoxFiles)
                    {
                        overwriteDecisions[file.BoxId] = true;
                    }
                }

                IsBusy = true;
                StatusMessage = $"Importing {selectedFiles.Count} file(s)...";

                // Convert to ImportFileInfo list
                var filesToImport = selectedFiles.Select(f => new ImportFileInfo
                {
                    FilePath = f.FilePath,
                    FileName = f.FileName,
                    BoxId = f.BoxId,
                    BoxName = f.BoxName,
                    BoxType = f.BoxType,
                    ComponentCount = f.ComponentCount,
                    ExportDate = f.ExportDate,
                    IsExistingBox = f.IsExistingBox,
                    IsSelected = true
                }).ToList();

                var result = await _exportImportService.ImportSelectedFiles(filesToImport, overwriteDecisions);

                if (result.Success)
                {
                    var message = $"Successfully imported {result.BoxesImported} box(es):\n";

                    if (result.BoxesOverwritten > 0)
                    {
                        message += $"↻ {result.BoxesOverwritten} overwritten\n";
                    }
                    if (result.BoxesCreated > 0)
                    {
                        message += $"✓ {result.BoxesCreated} created\n";
                    }

                    await ShowAlert("Import Complete", message);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await ShowAlert("Import Failed", result.ErrorMessage ?? "Unknown error occurred");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", $"Import failed: {ex.Message}");
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
            SelectedCount = AvailableFiles.Count(f => f.IsSelected);
            OverwriteCount = AvailableFiles.Count(f => f.IsSelected && f.IsExistingBox);
            NewBoxCount = AvailableFiles.Count(f => f.IsSelected && f.IsNewBox);
            HasSelection = SelectedCount > 0;
            HasOverwrites = OverwriteCount > 0;
            HasNewBoxes = NewBoxCount > 0;
        }

        private async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }

        private async Task<bool> ShowConfirm(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                return await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
            }
            return false;
        }
    }

    // Interface for platform-specific folder picker (to be implemented)
    public interface IFolderPickerService
    {
        Task<string?> PickExportFolderAsync();
    }
}