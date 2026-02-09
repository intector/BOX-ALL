namespace BOX_ALL.Models
{
    /// <summary>
    /// Represents a single compartment in a box
    /// </summary>
    public class Compartment
    {
        public string Position { get; set; } = "";  // L-01, K-02, etc.
        public ComponentData? Component { get; set; }

        // Helper properties
        public bool IsEmpty => Component == null;
        public bool IsOccupied => Component != null;
        public bool IsLowStock => Component != null &&
                                  Component.Quantity > 0 &&
                                  Component.Quantity <= Component.MinStock;
        public bool IsOutOfStock => Component != null && Component.Quantity == 0;
    }
}