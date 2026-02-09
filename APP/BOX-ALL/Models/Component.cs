using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Component model used for UI binding and business logic
    /// Maps to/from ComponentData for JSON storage
    /// </summary>
    public class Component
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? PartNumber { get; set; }

        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? Value { get; set; }

        public string? Package { get; set; }

        public string? Tolerance { get; set; }

        public string? Voltage { get; set; }

        public string? Manufacturer { get; set; }

        public string? Supplier { get; set; }

        public string? SupplierPartNumber { get; set; }

        public decimal UnitPrice { get; set; }

        public string? DatasheetUrl { get; set; }

        public string? Notes { get; set; }

        public string? SalesOrderNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Convert to ComponentData for JSON storage
        /// </summary>
        public ComponentData ToComponentData(int quantity = 0, int minStock = 10)
        {
            return new ComponentData
            {
                PartNumber = PartNumber ?? "",
                Description = Description ?? "",
                Manufacturer = Manufacturer ?? "",
                Category = Category ?? "Other",
                Quantity = quantity,
                MinStock = minStock,
                Supplier = Supplier ?? "",
                SupplierPartNumber = SupplierPartNumber ?? "",
                Value = Value ?? "",
                Package = Package ?? "",
                Tolerance = Tolerance ?? "",
                Voltage = Voltage ?? "",
                UnitPrice = UnitPrice,
                Notes = Notes ?? "",
                DatasheetUrl = DatasheetUrl ?? "",
                SalesOrderNumber = SalesOrderNumber ?? "",
                LastUpdated = UpdatedAt
            };
        }

        /// <summary>
        /// Create from ComponentData loaded from JSON
        /// </summary>
        public static Component FromComponentData(ComponentData data)
        {
            return new Component
            {
                PartNumber = data.PartNumber,
                Description = data.Description,
                Manufacturer = data.Manufacturer,
                Category = data.Category,
                Supplier = data.Supplier,
                SupplierPartNumber = data.SupplierPartNumber,
                Value = data.Value,
                Package = data.Package,
                Tolerance = data.Tolerance,
                Voltage = data.Voltage,
                UnitPrice = data.UnitPrice,
                Notes = data.Notes,
                DatasheetUrl = data.DatasheetUrl,
                SalesOrderNumber = data.SalesOrderNumber,
                CreatedAt = data.LastUpdated,
                UpdatedAt = data.LastUpdated
            };
        }
    }
}