using System;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Location model for backward compatibility with ViewModels
    /// Maps to Compartment in the JSON structure
    /// </summary>
    public class Location
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ComponentId { get; set; }

        public Guid BoxId { get; set; }

        public string Position { get; set; } = ""; // L-01, K-05, etc.

        public int Quantity { get; set; }

        public int MinQuantity { get; set; } = 10; // Default minimum before low stock alert

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Navigation properties (not stored in JSON)
        public Component? Component { get; set; }

        public Box? Box { get; set; }

        // Helper property to check if low stock
        public bool IsLowStock => Quantity > 0 && Quantity <= MinQuantity;

        public bool IsEmpty => Quantity == 0;

        /// <summary>
        /// Create Location from Compartment data
        /// </summary>
        public static Location FromCompartment(Compartment compartment, string boxId)
        {
            if (compartment.Component == null)
            {
                return new Location
                {
                    Position = compartment.Position,
                    Quantity = 0,
                    MinQuantity = 10
                };
            }

            var location = new Location
            {
                Position = compartment.Position,
                Quantity = compartment.Component.Quantity,
                MinQuantity = compartment.Component.MinStock,
                LastUpdated = compartment.Component.LastUpdated
            };

            // Set the Component property separately to avoid ambiguity
            location.Component = Models.Component.FromComponentData(compartment.Component);

            return location;
        }
    }
}