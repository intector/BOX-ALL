using CommunityToolkit.Mvvm.ComponentModel;

namespace BOX_ALL.Models
{
    public partial class BoxTypeOption : ObservableObject
    {
        public string TypeCode { get; set; } = "";          // "144", "96", "48", "40", "24"
        public string DisplayName { get; set; } = "";       // "BOXALL-144 (12×12 — 144 compartments)"
        public string SubText { get; set; } = "";           // "12 × 12 grid"
        public int TotalCompartments { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public bool IsEnabled { get; set; }
        public bool SupportsAntiStatic { get; set; }        // True for 144 and 96

        [ObservableProperty]
        private bool isSelected = false;

        public BoxTypeOption()
        {
        }

        public BoxTypeOption(string typeCode, int compartments, int rows, int cols, bool enabled, bool supportsAS)
        {
            TypeCode = typeCode;
            TotalCompartments = compartments;
            Rows = rows;
            Columns = cols;
            IsEnabled = enabled;
            SupportsAntiStatic = supportsAS;
            DisplayName = $"BOXALL-{typeCode}";
            SubText = enabled
                ? $"{rows} × {cols} grid — {compartments} compartments"
                : $"{compartments} compartments — Coming Soon";
        }
    }
}
