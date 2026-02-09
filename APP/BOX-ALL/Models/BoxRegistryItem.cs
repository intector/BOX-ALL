using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Represents an individual box entry in the registry
    /// </summary>
    public class BoxRegistryItem
    {
        public string Id { get; set; } = "";  // box_0001, box_0002, etc.
        public string Name { get; set; } = "";
        public string Type { get; set; } = "BOXALL144AS";  // BOXALL144AS, BOXALL96AS, etc.
        public string Filename { get; set; } = "";  // box_0001_main_storage.json
        public string Color { get; set; } = "#4A9EFF";
        public int SortOrder { get; set; }
        public int Rows { get; set; } = 12;
        public int Columns { get; set; } = 12;
        public int TotalCompartments { get; set; } = 144;
        public int OccupiedCompartments { get; set; }
        public int LowStockCount { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;

        // Helper method to generate compartment label
        public string GetCompartmentLabel(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                return "";

            // For 96-type: rows go J(0) to A(9), 10 rows
            // For 144-type: rows go L(0) to A(11), 12 rows
            char topRow = Type.Contains("96") ? 'J' : 'L';
            char rowLetter = (char)(topRow - row);
            string columnNumber = (column + 1).ToString("D2");
            return $"{rowLetter}-{columnNumber}";
        }

        // Get row and column from label like "L-01"
        public static (int row, int column) ParseCompartmentLabel(string label, string boxType = "BOXALL144")
        {
            if (string.IsNullOrEmpty(label) || label.Length < 4)
                return (-1, -1);

            var parts = label.Split('-');
            if (parts.Length != 2)
                return (-1, -1);

            char rowLetter = parts[0][0];
            char topRow = boxType.Contains("96") ? 'J' : 'L';
            int row = topRow - rowLetter;

            if (int.TryParse(parts[1], out int column))
            {
                return (row, column - 1); // Convert to 0-based index
            }

            return (-1, -1);
        }
    }
}