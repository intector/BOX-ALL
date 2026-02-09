using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BOX_ALL.Models
{
    public enum ImportRowStatus
    {
        Ready,           // ✅ BoxName + Position filled, box exists, position valid
        Conflict,        // ⚠️ Compartment already occupied
        Skip,            // ⭕ BoxName or Position empty
        AlreadyImported, // 🔄 Found in import log
        InvalidBox,      // ❌ BoxName doesn't match any existing box
        InvalidPosition, // ❌ Position not valid for box type
        Imported,        // ✅ Successfully imported (post-import)
        Skipped          // User chose not to overwrite (post-import)
    }

    public class BulkImportRow : INotifyPropertyChanged
    {
        private ImportRowStatus _status;

        public int RowNumber { get; set; }

        // Routing fields
        public string BoxName { get; set; } = "";
        public string Position { get; set; } = "";

        // Resolved box info (after validation)
        public string? BoxId { get; set; }
        public string? BoxType { get; set; }

        // ComponentData fields
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

        // Existing component at this position (if conflict)
        public string? ExistingPartNumber { get; set; }

        public ImportRowStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string StatusText => Status switch
        {
            ImportRowStatus.Ready => "✅ Ready",
            ImportRowStatus.Conflict => $"⚠️ Occupied ({ExistingPartNumber})",
            ImportRowStatus.Skip => "⭕ No position",
            ImportRowStatus.AlreadyImported => "🔄 Already imported",
            ImportRowStatus.InvalidBox => $"❌ Box \"{BoxName}\" not found",
            ImportRowStatus.InvalidPosition => $"❌ Invalid position \"{Position}\"",
            ImportRowStatus.Imported => "✅ Imported",
            ImportRowStatus.Skipped => "⏭️ Skipped",
            _ => ""
        };

        public string StatusColor => Status switch
        {
            ImportRowStatus.Ready => "#10B981",
            ImportRowStatus.Conflict => "#F59E0B",
            ImportRowStatus.Skip => "#64748B",
            ImportRowStatus.AlreadyImported => "#64748B",
            ImportRowStatus.InvalidBox => "#EF4444",
            ImportRowStatus.InvalidPosition => "#EF4444",
            ImportRowStatus.Imported => "#10B981",
            ImportRowStatus.Skipped => "#64748B",
            _ => "#FFFFFF"
        };

        /// <summary>
        /// Display label for the list: "PartNumber → BoxName:Position"
        /// </summary>
        public string DisplayLabel =>
            string.IsNullOrEmpty(Position)
                ? PartNumber
                : $"{PartNumber} → {BoxName}:{Position}";

        public ComponentData ToComponentData()
        {
            return new ComponentData
            {
                PartNumber = PartNumber,
                Description = Description,
                Manufacturer = Manufacturer,
                Category = string.IsNullOrEmpty(Category) ? "Other" : Category,
                Quantity = Quantity,
                MinStock = MinStock,
                Supplier = Supplier,
                SupplierPartNumber = SupplierPartNumber,
                Value = Value,
                Package = Package,
                Tolerance = Tolerance,
                Voltage = Voltage,
                UnitPrice = UnitPrice,
                Notes = Notes,
                DatasheetUrl = DatasheetUrl,
                SalesOrderNumber = SalesOrderNumber ?? "",
                LastUpdated = DateTime.Now
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
