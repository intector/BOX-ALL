using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BOX_ALL.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage;

namespace BOX_ALL.Services
{
    // Data model for export selection
    public partial class BoxExportItem : ObservableObject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int OccupiedCount { get; set; }
        public int TotalCompartments { get; set; }

        [ObservableProperty]
        private bool isSelected;

        public string Summary => $"{Name} | {Type} | {OccupiedCount} parts";
    }

    // Data model for import file info
    public class ImportFileInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string BoxId { get; set; } = "";
        public string BoxName { get; set; } = "";
        public string BoxType { get; set; } = "";
        public int ComponentCount { get; set; }
        public DateTime ExportDate { get; set; }
        public bool IsExistingBox { get; set; }
        public bool IsSelected { get; set; }
    }

    // Export format wrapper
    public class ExportedBox
    {
        public string ExportVersion { get; set; } = "1.0";
        public DateTime ExportDate { get; set; } = DateTime.Now;
        public BoxMetadata? BoxMetadata { get; set; }
        public BoxData? BoxData { get; set; }
    }

    public class BoxMetadata
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int TotalCompartments { get; set; }
    }

    // Results
    public class ExportResult
    {
        public bool Success { get; set; }
        public int BoxesExported { get; set; }
        public List<string> ExportedFiles { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }
        public string ExportPath { get; set; } = "";
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int BoxesImported { get; set; }
        public int BoxesOverwritten { get; set; }
        public int BoxesCreated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Recovery detection result
    public class ImportAccessResult
    {
        public bool HasDirectAccess { get; set; }
        public bool RequiresRecovery { get; set; }
        public List<ImportFileInfo> Files { get; set; } = new List<ImportFileInfo>();
        public string ExportFolderPath { get; set; } = "";
    }

    // Main Service
    public class ExportImportService
    {
        private readonly BoxRegistryService _boxRegistryService;
        private readonly BoxDataService _boxDataService;
        private readonly FileService _fileService;
        private readonly JsonSerializerOptions _jsonOptions;

        // SAF persistent URI storage
        private const string EXPORT_FOLDER_URI_KEY = "export_folder_uri";
        private const string EXPORT_FOLDER_CONNECTED_KEY = "export_folder_connected";

        public ExportImportService(BoxRegistryService boxRegistryService, BoxDataService boxDataService, FileService fileService)
        {
            _boxRegistryService = boxRegistryService;
            _boxDataService = boxDataService;
            _fileService = fileService;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        // Get all boxes available for export
        public async Task<List<BoxExportItem>> GetAvailableBoxesForExport()
        {
            var boxes = await _boxRegistryService.GetAllBoxesAsync();
            var exportItems = new List<BoxExportItem>();

            foreach (var box in boxes)
            {
                exportItems.Add(new BoxExportItem
                {
                    Id = box.Id,
                    Name = box.Name,
                    Type = box.Type,
                    OccupiedCount = box.OccupiedCompartments,
                    TotalCompartments = box.TotalCompartments,
                    IsSelected = false
                });
            }

            return exportItems;
        }

        // Export selected boxes
        public async Task<ExportResult> ExportSelectedBoxes(List<BoxExportItem> selectedBoxes)
        {
            var result = new ExportResult();

            try
            {
                // Get export path from FileService
                string exportPath = GetExportPath();
                result.ExportPath = exportPath;

                // Create directory if it doesn't exist
                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                foreach (var box in selectedBoxes)
                {
                    try
                    {
                        // Load the box data
                        var boxData = await _boxDataService.LoadBoxAsync(box.Id);
                        if (boxData == null)
                        {
                            Debug.WriteLine($"Could not load box {box.Id}");
                            continue;
                        }

                        // Create export wrapper
                        var exportedBox = new ExportedBox
                        {
                            ExportVersion = "1.0",
                            ExportDate = DateTime.Now,
                            BoxMetadata = new BoxMetadata
                            {
                                Id = box.Id,
                                Name = box.Name,
                                Type = box.Type,
                                Rows = 12,
                                Columns = 12,
                                TotalCompartments = box.TotalCompartments
                            },
                            BoxData = boxData
                        };

                        // Generate filename with timestamp
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                        string safeBoxName = SanitizeFilename(box.Name);
                        string filename = $"{box.Id}_{safeBoxName}_{timestamp}.json";
                        string filePath = Path.Combine(exportPath, filename);

                        // Serialize and save
                        string json = JsonSerializer.Serialize(exportedBox, _jsonOptions);
                        await File.WriteAllTextAsync(filePath, json);

                        result.ExportedFiles.Add(filename);
                        result.BoxesExported++;

                        Debug.WriteLine($"Exported box {box.Id} to {filename}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error exporting box {box.Id}: {ex.Message}");
                    }
                }

                result.Success = result.BoxesExported > 0;
                if (!result.Success)
                {
                    result.ErrorMessage = "No boxes were exported";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"Export error: {ex}");
            }

            return result;
        }

        // Enhanced method to check import access and detect recovery scenario
        public async Task<ImportAccessResult> CheckImportAccess()
        {
            var result = new ImportAccessResult
            {
                ExportFolderPath = GetExportPath()
            };

            try
            {
                // First, try direct access
                var directFiles = await GetAvailableImportFilesDirect();
                if (directFiles.Any())
                {
                    result.HasDirectAccess = true;
                    result.RequiresRecovery = false;
                    result.Files = directFiles;
                    return result;
                }

                // Check if we have a persisted SAF URI
                var hasPersistedUri = Preferences.ContainsKey(EXPORT_FOLDER_URI_KEY);
                if (hasPersistedUri)
                {
                    // Try to use persisted URI (platform-specific implementation)
#if ANDROID
                    var files = await TryAccessWithPersistedUri();
                    if (files.Any())
                    {
                        result.HasDirectAccess = true;
                        result.RequiresRecovery = false;
                        result.Files = files;
                        return result;
                    }
#endif
                }

                // Check if the export folder exists but we can't access it
                // This indicates a reinstall scenario
                result.RequiresRecovery = CheckIfExportFolderExists();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking import access: {ex.Message}");
            }

            return result;
        }

        // Original method - try direct file access
        private async Task<List<ImportFileInfo>> GetAvailableImportFilesDirect()
        {
            var importFiles = new List<ImportFileInfo>();

            try
            {
                string exportPath = GetExportPath();

                if (!Directory.Exists(exportPath))
                {
                    return importFiles;
                }

                var files = Directory.GetFiles(exportPath, "*.json");
                var boxes = await _boxRegistryService.GetAllBoxesAsync();

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var exportedBox = JsonSerializer.Deserialize<ExportedBox>(json, _jsonOptions);

                        if (exportedBox?.BoxMetadata != null)
                        {
                            var fileInfo = new ImportFileInfo
                            {
                                FilePath = file,
                                FileName = Path.GetFileName(file),
                                BoxId = exportedBox.BoxMetadata.Id,
                                BoxName = exportedBox.BoxMetadata.Name,
                                BoxType = exportedBox.BoxMetadata.Type,
                                ComponentCount = exportedBox.BoxData?.GetOccupiedCount() ?? 0,
                                ExportDate = exportedBox.ExportDate,
                                IsExistingBox = boxes.Any(b => b.Id == exportedBox.BoxMetadata.Id),
                                IsSelected = false
                            };

                            importFiles.Add(fileInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading import file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning for import files: {ex.Message}");
            }

            return importFiles;
        }

        // Public method that will be called from ViewModel
        public async Task<List<ImportFileInfo>> GetAvailableImportFiles()
        {
            // This will be called after recovery is handled
            // For now, return direct access attempt
            return await GetAvailableImportFilesDirect();
        }

        // Platform-specific: Try to access files with persisted URI
#if ANDROID
        private async Task<List<ImportFileInfo>> TryAccessWithPersistedUri()
        {
            try
            {
                var uriString = Preferences.Get(EXPORT_FOLDER_URI_KEY, "");
                if (!string.IsNullOrEmpty(uriString))
                {
                    // Try to read files using the persisted URI
                    var uri = Android.Net.Uri.Parse(uriString);
                    if (uri != null)
                    {
                        var currentActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                        if (currentActivity?.ContentResolver != null)
                        {
                            // Check if we still have permission
                            var persistedUris = currentActivity.ContentResolver.PersistedUriPermissions;
                            if (persistedUris?.Any(p => p.Uri?.ToString() == uriString) == true)
                            {
                                // Read files from the URI
                                return ReadFilesFromContentUri(uri, currentActivity.ContentResolver);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing persisted URI: {ex.Message}");
            }

            return new List<ImportFileInfo>();
        }

        public async Task<List<ImportFileInfo>> GetFilesFromSAFUri(string uriString)
        {
            var importFiles = new List<ImportFileInfo>();

            try
            {
                // Save the URI for future use
                Preferences.Set(EXPORT_FOLDER_URI_KEY, uriString);
                Preferences.Set(EXPORT_FOLDER_CONNECTED_KEY, true);

                var uri = Android.Net.Uri.Parse(uriString);
                if (uri != null)
                {
                    var currentActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                    if (currentActivity?.ContentResolver != null)
                    {
                        importFiles = ReadFilesFromContentUri(uri, currentActivity.ContentResolver);
                    }
                }

                // If no files found via SAF, try direct access again
                if (!importFiles.Any())
                {
                    importFiles = await GetAvailableImportFilesDirect();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing files via SAF: {ex.Message}");
            }

            return importFiles;
        }

        private List<ImportFileInfo> ReadFilesFromContentUri(Android.Net.Uri treeUri, Android.Content.ContentResolver contentResolver)
        {
            var files = new List<ImportFileInfo>();

            try
            {
                // Build child documents URI
                var childrenUri = Android.Provider.DocumentsContract.BuildChildDocumentsUriUsingTree(
                    treeUri,
                    Android.Provider.DocumentsContract.GetTreeDocumentId(treeUri)
                );

                // Query for all files
                var projection = new[]
                {
                    Android.Provider.DocumentsContract.Document.ColumnDisplayName,
                    Android.Provider.DocumentsContract.Document.ColumnDocumentId
                };

                using var cursor = contentResolver.Query(childrenUri, projection, null, null, null);

                if (cursor != null)
                {
                    while (cursor.MoveToNext())
                    {
                        var fileName = cursor.GetString(0);

                        // Only process JSON files
                        if (fileName?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            try
                            {
                                // Build URI for this specific file
                                var documentId = cursor.GetString(1);
                                var documentUri = Android.Provider.DocumentsContract.BuildDocumentUriUsingTree(treeUri, documentId);

                                // Read file content
                                using var inputStream = contentResolver.OpenInputStream(documentUri);
                                if (inputStream != null)
                                {
                                    using var reader = new System.IO.StreamReader(inputStream);
                                    var json = reader.ReadToEnd();

                                    // Parse the export file
                                    var exportedBox = JsonSerializer.Deserialize<ExportedBox>(json, _jsonOptions);

                                    if (exportedBox?.BoxMetadata != null)
                                    {
                                        // Check if box exists
                                        var boxes = _boxRegistryService.GetAllBoxesAsync().Result;

                                        var fileInfo = new ImportFileInfo
                                        {
                                            FilePath = documentUri.ToString(),
                                            FileName = fileName,
                                            BoxId = exportedBox.BoxMetadata.Id,
                                            BoxName = exportedBox.BoxMetadata.Name,
                                            BoxType = exportedBox.BoxMetadata.Type,
                                            ComponentCount = exportedBox.BoxData?.GetOccupiedCount() ?? 0,
                                            ExportDate = exportedBox.ExportDate,
                                            IsExistingBox = boxes.Any(b => b.Id == exportedBox.BoxMetadata.Id),
                                            IsSelected = false
                                        };

                                        files.Add(fileInfo);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error reading file {fileName}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading files from URI: {ex.Message}");
            }

            return files;
        }
#endif

        // Check if export folder exists (without trying to read it)
        private bool CheckIfExportFolderExists()
        {
            try
            {
                // We can check existence even without read permission
                var exportPath = GetExportPath();

                // This might return true even if we can't read the contents
                return Directory.Exists(exportPath);
            }
            catch
            {
                return false;
            }
        }

        // Import selected files
        public async Task<ImportResult> ImportSelectedFiles(List<ImportFileInfo> selectedFiles, Dictionary<string, bool> overwriteDecisions)
        {
            var result = new ImportResult();

            try
            {
                foreach (var file in selectedFiles)
                {
                    try
                    {
                        string json;

                        // Check if it's a content URI or regular file path
                        if (file.FilePath.StartsWith("content://"))
                        {
#if ANDROID
                            // Read from content URI
                            var uri = Android.Net.Uri.Parse(file.FilePath);
                            var currentActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                            if (currentActivity?.ContentResolver == null)
                            {
                                Debug.WriteLine($"Cannot read content URI without activity");
                                continue;
                            }

                            using var inputStream = currentActivity.ContentResolver.OpenInputStream(uri);
                            if (inputStream == null)
                            {
                                Debug.WriteLine($"Cannot open input stream for {file.FilePath}");
                                continue;
                            }

                            using var reader = new System.IO.StreamReader(inputStream);
                            json = reader.ReadToEnd();
#else
                            Debug.WriteLine($"Content URIs not supported on this platform");
                            continue;
#endif
                        }
                        else
                        {
                            // Regular file read
                            json = await File.ReadAllTextAsync(file.FilePath);
                        }

                        var exportedBox = JsonSerializer.Deserialize<ExportedBox>(json, _jsonOptions);

                        if (exportedBox?.BoxMetadata == null || exportedBox.BoxData == null)
                        {
                            Debug.WriteLine($"Invalid export file: {file.FileName}");
                            continue;
                        }

                        // Check if box exists
                        var existingBox = await _boxRegistryService.GetBoxAsync(exportedBox.BoxMetadata.Id);

                        string boxId = exportedBox.BoxMetadata.Id;

                        if (existingBox != null)
                        {
                            // Box exists - check overwrite decision
                            if (!overwriteDecisions.ContainsKey(boxId) || !overwriteDecisions[boxId])
                            {
                                Debug.WriteLine($"Skipping existing box: {boxId}");
                                continue;
                            }

                            // Overwrite existing box
                            exportedBox.BoxData.BoxId = boxId;
                            await _boxDataService.SaveBoxAsync(exportedBox.BoxData);
                            result.BoxesOverwritten++;
                        }
                        else
                        {
                            // Create new box  
                            var newBox = await _boxRegistryService.CreateBoxAsync(
                                exportedBox.BoxMetadata.Name,
                                exportedBox.BoxMetadata.Type
                            );

                            if (newBox != null)
                            {
                                // We need to delete the auto-generated box and create with correct ID
                                await _boxRegistryService.DeleteBoxAsync(newBox.Id);

                                // Create with correct ID
                                var registry = await _boxRegistryService.LoadRegistryAsync();
                                var importedBox = new BoxRegistryItem
                                {
                                    Id = boxId,
                                    Name = exportedBox.BoxMetadata.Name,
                                    Type = exportedBox.BoxMetadata.Type,
                                    Filename = $"{boxId}_{_fileService.SanitizeFilename(exportedBox.BoxMetadata.Name)}.json",
                                    Created = DateTime.Now,
                                    Modified = DateTime.Now,
                                    SortOrder = registry.Boxes.Count + 1,
                                    Rows = exportedBox.BoxMetadata.Rows,
                                    Columns = exportedBox.BoxMetadata.Columns,
                                    TotalCompartments = exportedBox.BoxMetadata.TotalCompartments,
                                    OccupiedCompartments = exportedBox.BoxData.GetOccupiedCount(),
                                    LowStockCount = exportedBox.BoxData.GetLowStockCount(),
                                    Color = "#4A9EFF"
                                };

                                registry.Boxes.Add(importedBox);
                                await _boxRegistryService.SaveRegistryAsync();

                                // Save the box data
                                await _boxDataService.SaveBoxAsync(exportedBox.BoxData);

                                result.BoxesCreated++;
                            }
                        }

                        result.BoxesImported++;
                        Debug.WriteLine($"Imported box {boxId}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error importing file {file.FileName}: {ex.Message}");
                    }
                }

                result.Success = result.BoxesImported > 0;
                if (!result.Success)
                {
                    result.ErrorMessage = "No boxes were imported";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"Import error: {ex}");
            }

            return result;
        }

        // Reset SAF connection (for Settings page)
        public void ResetFolderAccess()
        {
            Preferences.Remove(EXPORT_FOLDER_URI_KEY);
            Preferences.Remove(EXPORT_FOLDER_CONNECTED_KEY);
        }

        // Check if we have SAF connection
        public bool HasSAFConnection()
        {
            return Preferences.ContainsKey(EXPORT_FOLDER_URI_KEY) &&
                   Preferences.Get(EXPORT_FOLDER_CONNECTED_KEY, false);
        }

        private string GetExportPath()
        {
            // Use the export path from FileService
            return _fileService.GetExportPath();
        }

        private string SanitizeFilename(string filename)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", filename.Split(invalid));
            return sanitized.Replace(" ", "_").ToLower();
        }
    }
}