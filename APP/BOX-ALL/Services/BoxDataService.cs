using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Models;

namespace BOX_ALL.Services
{
    public class BoxDataService
    {
        private readonly FileService _fileService;
        private readonly BoxRegistryService _registryService;
        private BoxData? _currentBox;
        private string? _currentBoxId;

        public BoxDataService(FileService fileService, BoxRegistryService registryService)
        {
            _fileService = fileService;
            _registryService = registryService;
        }

        public async Task<BoxData?> LoadBoxAsync(string boxId)
        {
            try
            {
                // Check if already loaded
                if (_currentBoxId == boxId && _currentBox != null)
                    return _currentBox;

                // Get box info from registry
                var boxInfo = await _registryService.GetBoxAsync(boxId);
                if (boxInfo == null)
                {
                    Debug.WriteLine($"Box {boxId} not found in registry");
                    return null;
                }

                // Load box data
                var boxData = await _fileService.LoadJsonAsync<BoxData>($"boxes/{boxInfo.Filename}");

                if (boxData == null)
                {
                    // Create empty box data if file doesn't exist
                    boxData = CreateEmptyBoxData(boxInfo);
                    await SaveBoxAsync(boxData);
                }

                _currentBox = boxData;
                _currentBoxId = boxId;

                return boxData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading box {boxId}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveBoxAsync(BoxData boxData)
        {
            try
            {
                // Get box info from registry
                var boxInfo = await _registryService.GetBoxAsync(boxData.BoxId);
                if (boxInfo == null)
                {
                    Debug.WriteLine($"Box {boxData.BoxId} not found in registry");
                    return false;
                }

                boxData.LastModified = DateTime.Now;

                // Save box data
                bool saved = await _fileService.SaveJsonAsync($"boxes/{boxInfo.Filename}", boxData);

                if (saved)
                {
                    // Update statistics in registry
                    int occupiedCount = boxData.GetOccupiedCount();
                    int lowStockCount = boxData.GetLowStockCount();
                    await _registryService.UpdateBoxStatsAsync(boxData.BoxId, occupiedCount, lowStockCount);
                }

                return saved;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving box {boxData.BoxId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddComponentAsync(string boxId, string position, ComponentData component)
        {
            var boxData = await LoadBoxAsync(boxId);
            if (boxData == null) return false;

            var compartment = boxData.GetCompartment(position);
            if (compartment == null)
            {
                Debug.WriteLine($"Compartment {position} not found in box {boxId}");
                return false;
            }

            compartment.Component = component;
            component.LastUpdated = DateTime.Now;

            return await SaveBoxAsync(boxData);
        }

        public async Task<bool> UpdateComponentAsync(string boxId, string position, ComponentData component)
        {
            return await AddComponentAsync(boxId, position, component);
        }

        public async Task<bool> DeleteComponentAsync(string boxId, string position)
        {
            var boxData = await LoadBoxAsync(boxId);
            if (boxData == null) return false;

            var compartment = boxData.GetCompartment(position);
            if (compartment == null) return false;

            compartment.Component = null;

            return await SaveBoxAsync(boxData);
        }

        public async Task<bool> MoveComponentAsync(string boxId, string fromPosition, string toPosition)
        {
            var boxData = await LoadBoxAsync(boxId);
            if (boxData == null) return false;

            var fromCompartment = boxData.GetCompartment(fromPosition);
            var toCompartment = boxData.GetCompartment(toPosition);

            if (fromCompartment?.Component == null || toCompartment == null)
                return false;

            // Move component
            toCompartment.Component = fromCompartment.Component;
            fromCompartment.Component = null;

            return await SaveBoxAsync(boxData);
        }

        public async Task<ComponentData?> GetComponentAsync(string boxId, string position)
        {
            var boxData = await LoadBoxAsync(boxId);
            return boxData?.GetCompartment(position)?.Component;
        }

        public async Task<List<Compartment>> GetOccupiedCompartmentsAsync(string boxId)
        {
            var boxData = await LoadBoxAsync(boxId);
            if (boxData == null) return new List<Compartment>();

            return boxData.Compartments.Where(c => c.Component != null).ToList();
        }

        public async Task<Dictionary<string, object>> GetStatisticsAsync(string? boxId = null)
        {
            var stats = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(boxId))
            {
                // Get stats for all boxes
                var allBoxes = await _registryService.GetAllBoxesAsync();
                int totalComponents = 0;
                int totalOccupied = 0;
                int totalLowStock = 0;

                foreach (var box in allBoxes)
                {
                    totalOccupied += box.OccupiedCompartments;
                    totalLowStock += box.LowStockCount;
                }

                stats["TotalComponents"] = totalOccupied; // Each occupied slot has 1 component
                stats["TotalBoxes"] = allBoxes.Count;
                stats["OccupiedLocations"] = totalOccupied;
                stats["LowStockCount"] = totalLowStock;
                stats["OutOfStockCount"] = 0; // Would need to iterate through all boxes
            }
            else
            {
                // Get stats for specific box
                var boxData = await LoadBoxAsync(boxId);
                if (boxData != null)
                {
                    var occupied = boxData.Compartments.Where(c => c.Component != null).ToList();
                    stats["TotalComponents"] = occupied.Count;
                    stats["OccupiedLocations"] = occupied.Count;
                    stats["LowStockCount"] = occupied.Count(c => c.IsLowStock);
                    stats["OutOfStockCount"] = occupied.Count(c => c.IsOutOfStock);
                }
            }

            stats["UnreadAlerts"] = 0; // Alerts removed in JSON version

            return stats;
        }

        public async Task<List<ComponentData>> SearchComponentsAsync(string searchTerm, string? boxId = null)
        {
            var results = new List<ComponentData>();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return results;

            searchTerm = searchTerm.ToLower();

            if (!string.IsNullOrEmpty(boxId))
            {
                // Search specific box
                var boxData = await LoadBoxAsync(boxId);
                if (boxData != null)
                {
                    results = boxData.Compartments
                        .Where(c => c.Component != null)
                        .Select(c => c.Component!)
                        .Where(comp =>
                            comp.PartNumber.ToLower().Contains(searchTerm) ||
                            comp.Description.ToLower().Contains(searchTerm) ||
                            comp.Category.ToLower().Contains(searchTerm) ||
                            comp.Manufacturer.ToLower().Contains(searchTerm))
                        .ToList();
                }
            }
            else
            {
                // Search all boxes
                var allBoxes = await _registryService.GetAllBoxesAsync();
                foreach (var box in allBoxes)
                {
                    var boxData = await LoadBoxAsync(box.Id);
                    if (boxData != null)
                    {
                        var boxResults = boxData.Compartments
                            .Where(c => c.Component != null)
                            .Select(c => c.Component!)
                            .Where(comp =>
                                comp.PartNumber.ToLower().Contains(searchTerm) ||
                                comp.Description.ToLower().Contains(searchTerm) ||
                                comp.Category.ToLower().Contains(searchTerm) ||
                                comp.Manufacturer.ToLower().Contains(searchTerm));

                        results.AddRange(boxResults);
                    }
                }
            }

            return results;
        }

        private BoxData CreateEmptyBoxData(BoxRegistryItem boxInfo)
        {
            var boxData = new BoxData
            {
                Version = "1.0",
                BoxId = boxInfo.Id,
                LastModified = DateTime.Now,
                Compartments = new List<Compartment>()
            };

            // Create all compartments (empty)
            for (int row = 0; row < boxInfo.Rows; row++)
            {
                for (int col = 0; col < boxInfo.Columns; col++)
                {
                    var position = boxInfo.GetCompartmentLabel(row, col);
                    boxData.Compartments.Add(new Compartment
                    {
                        Position = position,
                        Component = null
                    });
                }
            }

            return boxData;
        }
    }
}