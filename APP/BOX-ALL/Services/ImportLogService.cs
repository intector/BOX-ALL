using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BOX_ALL.Models;

namespace BOX_ALL.Services
{
    public class ImportLogService
    {
        private readonly FileService _fileService;
        private ImportLog? _cachedLog;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ImportLogService(FileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>
        /// Import log now lives in app-private storage alongside box data.
        /// This survives app updates but gets cleared on uninstall (which is correct).
        /// </summary>
        private string GetLogFilePath()
        {
            var baseDir = Path.Combine(
                FileSystem.AppDataDirectory,
                "BOX-ALL");
            Directory.CreateDirectory(baseDir);
            return Path.Combine(baseDir, "import_log.json");
        }

        public async Task<ImportLog> LoadAsync()
        {
            if (_cachedLog != null) return _cachedLog;

            var path = GetLogFilePath();
            try
            {
                if (File.Exists(path))
                {
                    var json = await File.ReadAllTextAsync(path);
                    _cachedLog = JsonSerializer.Deserialize<ImportLog>(json, JsonOptions) ?? new ImportLog();
                }
                else
                {
                    _cachedLog = new ImportLog();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportLogService: Error loading log: {ex.Message}");
                _cachedLog = new ImportLog();
            }

            return _cachedLog;
        }

        public async Task SaveAsync(ImportLog log)
        {
            var path = GetLogFilePath();
            try
            {
                var json = JsonSerializer.Serialize(log, JsonOptions);
                await File.WriteAllTextAsync(path, json);
                _cachedLog = log;
                Debug.WriteLine($"ImportLogService: Saved {log.Imports.Count} entries to {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImportLogService: Error saving log: {ex.Message}");
            }
        }

        public async Task AddEntryAsync(ImportLogEntry entry)
        {
            var log = await LoadAsync();
            log.Imports.Add(entry);
            await SaveAsync(log);
        }

        /// <summary>
        /// Check if a part+position+box combination was already imported.
        /// </summary>
        public bool IsAlreadyImported(ImportLog log, string partNumber, string position, string boxId)
        {
            return log.Imports.Any(e =>
                e.PartNumber.Equals(partNumber, StringComparison.OrdinalIgnoreCase) &&
                e.Position.Equals(position, StringComparison.OrdinalIgnoreCase) &&
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Remove import log entry for a specific compartment (when component is deleted).
        /// </summary>
        public async Task RemoveEntryAsync(string boxId, string position)
        {
            var log = await LoadAsync();
            int removed = log.Imports.RemoveAll(e =>
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase) &&
                e.Position.Equals(position, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                Debug.WriteLine($"ImportLogService: Removed {removed} entry(ies) for {boxId}:{position}");
                await SaveAsync(log);
            }
        }

        /// <summary>
        /// Remove all import log entries for a box (when box is deleted).
        /// </summary>
        public async Task RemoveEntriesByBoxAsync(string boxId)
        {
            var log = await LoadAsync();
            int removed = log.Imports.RemoveAll(e =>
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                Debug.WriteLine($"ImportLogService: Removed {removed} entry(ies) for box {boxId}");
                await SaveAsync(log);
            }
        }

        /// <summary>
        /// Update position for a relocated component. Also removes any entry
        /// at the destination position (in case it was overwritten).
        /// </summary>
        public async Task UpdatePositionAsync(string boxId, string oldPosition, string newPosition)
        {
            var log = await LoadAsync();
            bool changed = false;

            // Remove any entry at the destination (it's being overwritten)
            int removed = log.Imports.RemoveAll(e =>
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase) &&
                e.Position.Equals(newPosition, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) changed = true;

            // Update the relocated component's position
            var entry = log.Imports.FirstOrDefault(e =>
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase) &&
                e.Position.Equals(oldPosition, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                entry.Position = newPosition;
                changed = true;
                Debug.WriteLine($"ImportLogService: Updated position {oldPosition} → {newPosition} in box {boxId}");
            }

            if (changed)
            {
                await SaveAsync(log);
            }
        }

        /// <summary>
        /// Update box name in all import log entries for a box (when box is renamed).
        /// </summary>
        public async Task UpdateBoxNameAsync(string boxId, string newBoxName)
        {
            var log = await LoadAsync();
            bool changed = false;

            foreach (var entry in log.Imports.Where(e =>
                e.BoxId.Equals(boxId, StringComparison.OrdinalIgnoreCase)))
            {
                entry.BoxName = newBoxName;
                changed = true;
            }

            if (changed)
            {
                Debug.WriteLine($"ImportLogService: Updated box name to '{newBoxName}' for box {boxId}");
                await SaveAsync(log);
            }
        }

        /// <summary>
        /// Clear the cache so next LoadAsync() re-reads from disk.
        /// </summary>
        public void InvalidateCache()
        {
            _cachedLog = null;
        }
    }
}
