using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BOX_ALL.Models;

namespace BOX_ALL.Services
{
    /// <summary>
    /// Generates a JSON snapshot of all box data for Script Lab integration.
    /// Output: /storage/emulated/0/Documents/BOX-ALL/exports/boxall_status.json
    /// </summary>
    public class SyncExportService
    {
        private readonly BoxRegistryService _boxRegistryService;
        private readonly BoxDataService _boxDataService;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public SyncExportService(BoxRegistryService boxRegistryService, BoxDataService boxDataService)
        {
            _boxRegistryService = boxRegistryService;
            _boxDataService = boxDataService;
        }

        /// <summary>
        /// Export a full status snapshot to the public Documents folder.
        /// Returns the file path on success, or throws on failure.
        /// </summary>
        public async Task<string> ExportStatusAsync()
        {
            var allBoxes = await _boxRegistryService.GetAllBoxesAsync();

            // Master categories list (matches AddComponentViewModel.LoadCategories)
            var allCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Resistor", "Capacitor", "Inductor", "Diode", "LED",
                "Transistor", "MOSFET", "IC", "Microcontroller", "Crystal",
                "Connector", "Switch", "Button", "Relay", "Fuse",
                "Voltage Regulator", "Op-Amp", "Sensor", "Display", "Other"
            };

            var exportBoxes = new List<SyncExportBox>();

            foreach (var box in allBoxes)
            {
                var boxData = await _boxDataService.LoadBoxAsync(box.Id);
                if (boxData == null) continue;

                var occupiedCompartments = new List<SyncExportCompartment>();

                foreach (var compartment in boxData.Compartments)
                {
                    if (compartment.Component == null) continue;

                    var comp = compartment.Component;

                    // Also capture any in-use categories not in the master list
                    var cat = comp.Category ?? "";
                    if (cat.Length > 0)
                        allCategories.Add(cat);

                    occupiedCompartments.Add(new SyncExportCompartment
                    {
                        Position = compartment.Position ?? "",
                        PartNumber = comp.PartNumber ?? "",
                        Description = comp.Description ?? "",
                        Manufacturer = comp.Manufacturer ?? "",
                        Category = comp.Category ?? "",
                        Quantity = comp.Quantity,
                        MinStock = comp.MinStock,
                        Value = comp.Value ?? "",
                        Package = comp.Package ?? "",
                        Supplier = comp.Supplier ?? "",
                        SupplierPartNumber = comp.SupplierPartNumber ?? "",
                        UnitPrice = comp.UnitPrice,
                        Notes = comp.Notes ?? "",
                        SalesOrderNumber = comp.SalesOrderNumber ?? ""
                    });
                }

                exportBoxes.Add(new SyncExportBox
                {
                    BoxId = box.Id,
                    Name = box.Name,
                    Type = box.Type,
                    Rows = box.Rows,
                    Columns = box.Columns,
                    TotalCompartments = box.TotalCompartments,
                    OccupiedCount = occupiedCompartments.Count,
                    Compartments = occupiedCompartments
                });
            }

            var exportData = new SyncExportData
            {
                ExportDate = DateTime.UtcNow.ToString("o"),
                AppVersion = "1.0.0",
                Boxes = exportBoxes,
                Categories = allCategories
                    .OrderBy(c => c == "Other" ? 1 : 0)
                    .ThenBy(c => c)
                    .ToList()
            };

            // Write to public Documents folder
            var exportDir = Path.Combine(
                "/storage/emulated/0",
                "Documents",
                "BOX-ALL",
                "exports");
            Directory.CreateDirectory(exportDir);

            var filePath = Path.Combine(exportDir, "boxall_status.json");
            var json = JsonSerializer.Serialize(exportData, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            // Notify Android MediaStore so the file appears in file managers
#if ANDROID
            var context = Android.App.Application.Context;
            Android.Media.MediaScannerConnection.ScanFile(
                context,
                new[] { filePath },
                new[] { "application/json" },
                null);
#endif

            Debug.WriteLine($"SyncExportService: Exported {exportBoxes.Count} box(es), {allCategories.Count} categories to {filePath}");
            return filePath;
        }
    }
}
