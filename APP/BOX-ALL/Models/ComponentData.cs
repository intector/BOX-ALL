using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Component data stored in compartments (for JSON storage)
    /// </summary>
    public class ComponentData
    {
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Category { get; set; } = "Other";
        public int Quantity { get; set; }
        public int MinStock { get; set; } = 10;
        public string Supplier { get; set; } = "";
        public string SupplierPartNumber { get; set; } = "";
        public string Value { get; set; } = "";
        public string Package { get; set; } = "";
        public string Tolerance { get; set; } = "";
        public string Voltage { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public string Notes { get; set; } = "";
        public string DatasheetUrl { get; set; } = "";
        public string SalesOrderNumber { get; set; } = "";
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}