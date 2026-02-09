using System;
using System.Collections.Generic;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Root object for the sync export JSON snapshot.
    /// Written to /Documents/BOX-ALL/export/boxall_status.json
    /// </summary>
    public class SyncExportData
    {
        public string ExportDate { get; set; } = "";
        public string AppVersion { get; set; } = "1.0.0";
        public List<SyncExportBox> Boxes { get; set; } = new();
        public List<string> Categories { get; set; } = new();
    }

    public class SyncExportBox
    {
        public string BoxId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int TotalCompartments { get; set; }
        public int OccupiedCount { get; set; }
        public List<SyncExportCompartment> Compartments { get; set; } = new();
    }

    public class SyncExportCompartment
    {
        public string Position { get; set; } = "";
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Category { get; set; } = "";
        public int Quantity { get; set; }
        public int MinStock { get; set; }
        public string Value { get; set; } = "";
        public string Package { get; set; } = "";
        public string Supplier { get; set; } = "";
        public string SupplierPartNumber { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public string Notes { get; set; } = "";
        public string SalesOrderNumber { get; set; } = "";
    }
}
