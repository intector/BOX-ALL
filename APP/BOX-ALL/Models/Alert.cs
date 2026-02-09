using System;
using System.ComponentModel;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Alert model - kept for potential future use
    /// Could be stored in BoxData if needed
    /// </summary>
    public class Alert
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ComponentId { get; set; }

        public Guid LocationId { get; set; }

        public string AlertType { get; set; } = AlertTypes.Custom; // "LowStock", "OutOfStock", "Custom"

        public string Message { get; set; } = "";

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReadAt { get; set; }

        // Navigation properties
        public Component? Component { get; set; }

        public Models.Location? Location { get; set; }
    }

    // Alert type constants
    public static class AlertTypes
    {
        public const string LowStock = "LowStock";
        public const string OutOfStock = "OutOfStock";
        public const string Custom = "Custom";
    }
}