using System;
using System.Collections.Generic;

namespace BOX_ALL.Models
{
    public class ImportLog
    {
        public string Version { get; set; } = "1.0";
        public List<ImportLogEntry> Imports { get; set; } = new();
    }

    public class ImportLogEntry
    {
        public string PartNumber { get; set; } = "";
        public string SupplierPartNumber { get; set; } = "";
        public string BoxName { get; set; } = "";
        public string BoxId { get; set; } = "";
        public string Position { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime ImportDate { get; set; }
        public string SourceFile { get; set; } = "";
        public bool Overwritten { get; set; }
    }
}
