using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Represents the app settings stored in settings.json
    /// </summary>
    public class AppSettings
    {
        public string Version { get; set; } = "1.0";
        public string LastOpenedBoxId { get; set; } = "box_0001";
        public string Theme { get; set; } = "dark";
        public int DefaultMinStock { get; set; } = 10;
        public string DefaultCategory { get; set; } = "Other";
        public bool ShowLowStockWarnings { get; set; } = true;
        public bool AutoBackup { get; set; } = false;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}