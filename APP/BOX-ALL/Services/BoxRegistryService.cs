using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Models;

namespace BOX_ALL.Services
{
    public class BoxRegistryService
    {
        private readonly FileService _fileService;
        private BoxRegistry? _registry;
        private const string REGISTRY_FILE = "boxes.json";

        // Box type encoding map - maps type string to base hex value
        private readonly Dictionary<string, int> _boxTypeMap = new()
        {
            { "BOXALL24", 0x0200 },
            { "BOXALL40", 0x0400 },
            { "BOXALL48", 0x0800 },
            { "BOXALL96", 0x1000 },
            { "BOXALL144", 0x2000 },
            { "BOXALL96AS", 0x9000 },
            { "BOXALL144AS", 0xa000 }
        };

        public BoxRegistryService(FileService fileService)
        {
            _fileService = fileService;
        }

        public async Task<BoxRegistry> LoadRegistryAsync()
        {
            if (_registry != null)
                return _registry;

            _registry = await _fileService.LoadJsonAsync<BoxRegistry>(REGISTRY_FILE);

            if (_registry == null)
            {
                // Create default registry with one box
                _registry = CreateDefaultRegistry();
                await SaveRegistryAsync();
            }
            else if (_registry.Boxes.Count == 0)
            {
                /*
                // Registry exists but has no boxes - add default box
                var defaultBox = new BoxRegistryItem
                {
                    Id = "box_a000", // First BOXALL144AS box
                    Name = "Main Storage",
                    Type = "BOXALL144AS",
                    Filename = "box_a000_main_storage.json",
                    SortOrder = 1,
                    Rows = 12,
                    Columns = 12,
                    TotalCompartments = 144,
                    OccupiedCompartments = 0,
                    LowStockCount = 0,
                    Color = "#4A9EFF"
                };
                _registry.Boxes.Add(defaultBox);
                await SaveRegistryAsync();
                */
            }

            return _registry;
        }

        public async Task<bool> SaveRegistryAsync()
        {
            if (_registry == null) return false;

            _registry.LastModified = DateTime.Now;
            return await _fileService.SaveJsonAsync(REGISTRY_FILE, _registry);
        }

        public async Task<BoxRegistryItem?> GetBoxAsync(string boxId)
        {
            var registry = await LoadRegistryAsync();
            return registry.Boxes.FirstOrDefault(b => b.Id == boxId);
        }

        public async Task<List<BoxRegistryItem>> GetAllBoxesAsync()
        {
            var registry = await LoadRegistryAsync();
            return registry.Boxes.OrderBy(b => b.Name).ToList();
        }

        public async Task<BoxRegistryItem> CreateBoxAsync(string name, string type = "BOXALL144AS")
        {
            var registry = await LoadRegistryAsync();

            // Generate new box ID with type encoding
            var nextId = await GenerateNextBoxId(type);

            // Sanitize name for filename
            var sanitizedName = _fileService.SanitizeFilename(name);
            var filename = $"{nextId}_{sanitizedName}.json";

            var newBox = new BoxRegistryItem
            {
                Id = nextId,
                Name = name,
                Type = type,
                Filename = filename,
                SortOrder = registry.Boxes.Count + 1,
                Rows = type == "BOXALL96AS" || type == "BOXALL96" ? 10 : 12,
                Columns = 12,
                TotalCompartments = type == "BOXALL96AS" || type == "BOXALL96" ? 96 : 144,
                Color = "#4A9EFF"
            };

            registry.Boxes.Add(newBox);
            await SaveRegistryAsync();

            return newBox;
        }

        public async Task<bool> DeleteBoxAsync(string boxId)
        {
            var registry = await LoadRegistryAsync();
            var box = registry.Boxes.FirstOrDefault(b => b.Id == boxId);

            if (box == null) return false;

            // Delete box data file
            await _fileService.DeleteFileAsync($"boxes/{box.Filename}");

            // Remove from registry
            registry.Boxes.Remove(box);

            // Reorder remaining boxes
            var sortOrder = 1;
            foreach (var b in registry.Boxes.OrderBy(b => b.SortOrder))
            {
                b.SortOrder = sortOrder++;
            }

            await SaveRegistryAsync();
            return true;
        }

        public async Task<bool> UpdateBoxStatsAsync(string boxId, int occupiedCount, int lowStockCount)
        {
            var registry = await LoadRegistryAsync();
            var box = registry.Boxes.FirstOrDefault(b => b.Id == boxId);

            if (box == null) return false;

            box.OccupiedCompartments = occupiedCount;
            box.LowStockCount = lowStockCount;

            await SaveRegistryAsync();
            return true;
        }

        public async Task<bool> RenameBoxAsync(string boxId, string newName)
        {
            var registry = await LoadRegistryAsync();
            var box = registry.Boxes.FirstOrDefault(b => b.Id == boxId);

            if (box == null) return false;

            // Update box name
            box.Name = newName;

            // Update filename
            var oldFilename = box.Filename;
            var sanitizedName = _fileService.SanitizeFilename(newName);
            box.Filename = $"{boxId}_{sanitizedName}.json";

            // Rename actual file
            await _fileService.RenameFileAsync($"boxes/{oldFilename}", $"boxes/{box.Filename}");

            await SaveRegistryAsync();
            return true;
        }

        private async Task<string> GenerateNextBoxId(string boxType)
        {
            // Get the base value for this box type
            if (!_boxTypeMap.TryGetValue(boxType.Replace("-", "").ToUpper(), out int baseValue))
            {
                // Default to BOXALL144AS if type not recognized
                Debug.WriteLine($"Unknown box type: {boxType}, defaulting to BOXALL144AS");
                baseValue = 0xa000;
            }

            var registry = await LoadRegistryAsync();

            // Find all boxes of this type and extract their counters
            int maxCounter = -1;
            foreach (var box in registry.Boxes)
            {
                if (box.Id.StartsWith("box_") && box.Id.Length == 8)
                {
                    // Parse the hex ID
                    if (int.TryParse(box.Id.Substring(4), System.Globalization.NumberStyles.HexNumber, null, out int boxValue))
                    {
                        // Check if this box is of the same type (upper 7 bits match)
                        if ((boxValue & 0xFE00) == baseValue)
                        {
                            // Extract the counter (lower 9 bits)
                            int counter = boxValue & 0x01FF;
                            if (counter > maxCounter)
                            {
                                maxCounter = counter;
                            }
                        }
                    }
                }
            }

            // Increment counter for next box
            int nextCounter = maxCounter + 1;

            // Check if we've exceeded the maximum counter (511)
            if (nextCounter > 0x1FF)
            {
                throw new InvalidOperationException($"Maximum number of boxes (511) reached for type {boxType}");
            }

            // Combine type and counter to create the box ID
            int nextValue = baseValue | nextCounter;

            // Format as box_xxxx where xxxx is 4-digit lowercase hex
            return $"box_{nextValue:x4}";
        }

        private BoxRegistry CreateDefaultRegistry()
        {
            var registry = new BoxRegistry
            {
                Version = "1.0",
                LastModified = DateTime.Now,
                NextId = "0001", // Keep for backward compatibility but not used
                Boxes = new List<BoxRegistryItem>()
            };

            /*
            // Create a default box on first run
            var defaultBox = new BoxRegistryItem
            {
                Id = "box_a000", // First BOXALL144AS box
                Name = "Main Storage",
                Type = "BOXALL144AS",
                Filename = "box_a000_main_storage.json",
                SortOrder = 1,
                Rows = 12,
                Columns = 12,
                TotalCompartments = 144,
                OccupiedCompartments = 0,
                LowStockCount = 0,
                Color = "#4A9EFF"
            };

            registry.Boxes.Add(defaultBox);
            */
            return registry;
        }

        // Helper method to decode a box ID for debugging/info
        public (string type, int counter) DecodeBoxId(string boxId)
        {
            if (!boxId.StartsWith("box_") || boxId.Length != 8)
                return ("unknown", -1);

            if (int.TryParse(boxId.Substring(4), System.Globalization.NumberStyles.HexNumber, null, out int value))
            {
                int typeCode = value & 0xFE00;
                int counter = value & 0x01FF;

                // Reverse lookup the type
                var type = _boxTypeMap.FirstOrDefault(x => x.Value == typeCode).Key ?? "unknown";

                return (type, counter);
            }

            return ("unknown", -1);
        }
    }
}