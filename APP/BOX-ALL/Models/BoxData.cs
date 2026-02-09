using System;
using System.Collections.Generic;
using System.Linq;

namespace BOX_ALL.Models
{
    /// <summary>
    /// Represents the data for an individual box file (box_xxxx_name.json)
    /// </summary>
    public class BoxData
    {
        public string Version { get; set; } = "1.0";
        public string BoxId { get; set; } = "";  // box_0001, box_0002, etc.
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<Compartment> Compartments { get; set; } = new List<Compartment>();

        /// <summary>
        /// Get compartment by position (e.g., "L-01")
        /// </summary>
        public Compartment? GetCompartment(string position)
        {
            return Compartments?.FirstOrDefault(c => c.Position == position);
        }

        /// <summary>
        /// Set or update component in compartment
        /// </summary>
        public void SetComponent(string position, ComponentData? component)
        {
            var compartment = GetCompartment(position);
            if (compartment != null)
            {
                compartment.Component = component;
                if (component != null)
                {
                    component.LastUpdated = DateTime.Now;
                }
                LastModified = DateTime.Now;
            }
        }

        /// <summary>
        /// Count occupied compartments
        /// </summary>
        public int GetOccupiedCount()
        {
            return Compartments?.Count(c => c.Component != null) ?? 0;
        }

        /// <summary>
        /// Count low stock compartments
        /// </summary>
        public int GetLowStockCount()
        {
            return Compartments?.Count(c =>
                c.Component != null &&
                c.Component.Quantity > 0 &&
                c.Component.Quantity <= c.Component.MinStock) ?? 0;
        }
    }
}