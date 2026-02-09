using System;
using System.Collections.Generic;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Represents the main boxes.json registry file
    /// </summary>
    public class BoxRegistry
    {
        public string Version { get; set; } = "1.0";
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string NextId { get; set; } = "0001";
        public List<BoxRegistryItem> Boxes { get; set; } = new List<BoxRegistryItem>();
    }
}