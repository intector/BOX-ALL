using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Box model for backward compatibility with ViewModels
    /// Maps to BoxRegistryItem in the JSON structure
    /// </summary>
    public class Box
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string BoxType { get; set; } = "BOXALL144AS"; // BOXALL144AS, BOXALL96AS, etc.

        public string Name { get; set; } = "Main Storage Box"; // User-friendly name

        public int Rows { get; set; } = 12; // 12 for 144 compartments, 8 for 96

        public int Columns { get; set; } = 12; // Always 12 columns

        public string Color { get; set; } = "#4A9EFF"; // Hex color for visual identification

        public int SortOrder { get; set; } // For displaying boxes in specific order

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Helper method to get total compartments
        public int TotalCompartments { get; set; } = 144;

        // Helper method to generate compartment labels
        public string GetCompartmentLabel(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                return "";

            // Letters go from L (top) to A (bottom)
            char rowLetter = (char)('L' - row);
            string columnNumber = (column + 1).ToString("D2");
            return $"{rowLetter}-{columnNumber}";
        }

        // Get row and column from label like "L-01"
        public static (int row, int column) ParseCompartmentLabel(string label)
        {
            if (string.IsNullOrEmpty(label) || label.Length < 4)
                return (-1, -1);

            var parts = label.Split('-');
            if (parts.Length != 2)
                return (-1, -1);

            char rowLetter = parts[0][0];
            int row = 'L' - rowLetter;

            if (int.TryParse(parts[1], out int column))
            {
                return (row, column - 1); // Convert to 0-based index
            }

            return (-1, -1);
        }

        /// <summary>
        /// Create Box from BoxRegistryItem
        /// </summary>
        public static Box FromBoxRegistryItem(BoxRegistryItem item)
        {
            // Parse the box ID to create a Guid (just for compatibility)
            // We'll use a deterministic GUID based on the string ID
            var guidBytes = new byte[16];
            var idBytes = System.Text.Encoding.UTF8.GetBytes(item.Id);
            Array.Copy(idBytes, 0, guidBytes, 0, Math.Min(idBytes.Length, 16));

            return new Box
            {
                Id = new Guid(guidBytes),
                BoxType = item.Type,
                Name = item.Name,
                Rows = item.Rows,
                Columns = item.Columns,
                TotalCompartments = item.TotalCompartments,
                Color = item.Color,
                SortOrder = item.SortOrder,
                CreatedAt = item.Created,
                UpdatedAt = item.Modified
            };
        }

        /// <summary>
        /// Get the string box ID (e.g., "box_0001")
        /// </summary>
        public string GetBoxId()
        {
            // For now, we'll use the first box if this is called
            // In practice, we should track this properly
            return "box_0001";
        }
    }
}