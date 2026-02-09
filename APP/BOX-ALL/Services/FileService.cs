using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace BOX_ALL.Services
{
    public class FileService
    {
        private readonly string _basePath;
        private readonly string _exportPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileService()
        {
            // Use app-private storage for working data
            // This gets deleted on uninstall but avoids all permission issues
            _basePath = Path.Combine(
                FileSystem.AppDataDirectory,
                "BOX-ALL"
            );

            // Keep exports in external storage so they survive app uninstall
            _exportPath = Path.Combine(
                "/storage/emulated/0",
                "Documents",
                "BOX-ALL",
                "exports"
            );

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            EnsureDirectoryStructure();
            EnsureExportDirectory();
        }

        public string GetBasePath() => _basePath;
        public string GetExportPath() => _exportPath;

        private void EnsureDirectoryStructure()
        {
            try
            {
                // Create main directory in app-private storage
                Directory.CreateDirectory(_basePath);

                // Create subdirectories for working data
                Directory.CreateDirectory(Path.Combine(_basePath, "boxes"));
                Directory.CreateDirectory(Path.Combine(_basePath, "backups"));

                Debug.WriteLine($"Directory structure ensured at: {_basePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating directory structure: {ex.Message}");
            }
        }

        private void EnsureExportDirectory()
        {
            try
            {
                // Try to create export directory in external storage
                // This might fail due to permissions, but that's okay - SAF will handle it
                Directory.CreateDirectory(_exportPath);
                Debug.WriteLine($"Export directory ensured at: {_exportPath}");
            }
            catch (Exception ex)
            {
                // It's okay if this fails, exports will use SAF if needed
                Debug.WriteLine($"Could not create export directory (will use SAF if needed): {ex.Message}");
            }
        }

        public async Task<T?> LoadJsonAsync<T>(string relativePath) where T : class
        {
            try
            {
                var fullPath = Path.Combine(_basePath, relativePath);

                if (!File.Exists(fullPath))
                {
                    Debug.WriteLine($"File not found: {fullPath}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(fullPath);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading JSON from {relativePath}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveJsonAsync<T>(string relativePath, T data) where T : class
        {
            try
            {
                var fullPath = Path.Combine(_basePath, relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(fullPath, json);

                Debug.WriteLine($"Saved JSON to: {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving JSON to {relativePath}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(_basePath, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Debug.WriteLine($"Deleted file: {fullPath}");
                    return true;
                }

                Debug.WriteLine($"File not found for deletion: {fullPath}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting file {relativePath}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string oldRelativePath, string newRelativePath)
        {
            try
            {
                var oldFullPath = Path.Combine(_basePath, oldRelativePath);
                var newFullPath = Path.Combine(_basePath, newRelativePath);

                if (!File.Exists(oldFullPath))
                {
                    Debug.WriteLine($"Source file not found for rename: {oldFullPath}");
                    return false;
                }

                // Ensure the destination directory exists
                var newDirectory = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(newDirectory))
                {
                    Directory.CreateDirectory(newDirectory);
                }

                // Check if destination already exists
                if (File.Exists(newFullPath))
                {
                    Debug.WriteLine($"Destination file already exists: {newFullPath}");
                    return false;
                }

                File.Move(oldFullPath, newFullPath);
                Debug.WriteLine($"Renamed file from {oldFullPath} to {newFullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error renaming file from {oldRelativePath} to {newRelativePath}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(_basePath, relativePath);
                return File.Exists(fullPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking file existence for {relativePath}: {ex.Message}");
                return false;
            }
        }

        public async Task<string[]> GetFilesAsync(string relativePath, string searchPattern = "*")
        {
            try
            {
                var fullPath = Path.Combine(_basePath, relativePath);

                if (!Directory.Exists(fullPath))
                {
                    Debug.WriteLine($"Directory not found: {fullPath}");
                    return Array.Empty<string>();
                }

                var files = Directory.GetFiles(fullPath, searchPattern);

                // Return relative paths
                for (int i = 0; i < files.Length; i++)
                {
                    files[i] = Path.GetRelativePath(_basePath, files[i]);
                }

                return files;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting files from {relativePath}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public string SanitizeFilename(string filename)
        {
            // Remove invalid characters
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                filename = filename.Replace(c, '_');
            }

            // Replace spaces with underscores
            filename = filename.Replace(' ', '_');

            // Remove multiple underscores
            while (filename.Contains("__"))
            {
                filename = filename.Replace("__", "_");
            }

            // Limit length
            if (filename.Length > 50)
            {
                filename = filename.Substring(0, 50);
            }

            return filename.ToLower();
        }
    }
}