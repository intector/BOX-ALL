using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Helpers;
using BOX_ALL.Models;

namespace BOX_ALL.Services
{
    public class BulkCsvParserService
    {
        private readonly BoxRegistryService _boxRegistryService;
        private readonly BoxDataService _boxDataService;
        private readonly ImportLogService _importLogService;

        public BulkCsvParserService(
            BoxRegistryService boxRegistryService,
            BoxDataService boxDataService,
            ImportLogService importLogService)
        {
            _boxRegistryService = boxRegistryService;
            _boxDataService = boxDataService;
            _importLogService = importLogService;
        }

        /// <summary>
        /// Parse a BOX-ALL CSV file and validate each row against existing boxes and import log.
        /// </summary>
        public async Task<List<BulkImportRow>> ParseAndValidateAsync(Stream stream, string sourceFileName)
        {
            var rows = new List<BulkImportRow>();

            using var reader = new StreamReader(stream);

            var headerLine = await reader.ReadLineAsync();
            if (headerLine == null) return rows;

            var headers = ParseCsvLine(headerLine);
            var columnMap = BuildColumnMap(headers);

            Debug.WriteLine($"BulkCsvParser: Found {headers.Count} columns");

            // Load box registry for validation
            var registry = await _boxRegistryService.LoadRegistryAsync();
            var boxLookup = registry.Boxes.ToDictionary(b => b.Name, b => b, StringComparer.OrdinalIgnoreCase);

            // Load import log
            var importLog = await _importLogService.LoadAsync();

            int rowNumber = 0;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                var row = MapToImportRow(fields, columnMap, rowNumber);

                await ValidateRow(row, boxLookup, importLog, sourceFileName);

                rows.Add(row);
            }

            Debug.WriteLine($"BulkCsvParser: Parsed {rows.Count} rows");
            return rows;
        }

        private BulkImportRow MapToImportRow(List<string> fields, Dictionary<string, int> columnMap, int rowNumber)
        {
            return new BulkImportRow
            {
                RowNumber = rowNumber,
                BoxName = GetField(fields, columnMap, "BoxName"),
                Position = GetField(fields, columnMap, "Position").ToUpperInvariant(),
                PartNumber = GetField(fields, columnMap, "PartNumber"),
                Description = GetField(fields, columnMap, "Description"),
                Manufacturer = GetField(fields, columnMap, "Manufacturer"),
                Category = GetField(fields, columnMap, "Category", "Other"),
                Quantity = ParseInt(GetField(fields, columnMap, "Quantity"), 0),
                MinStock = ParseInt(GetField(fields, columnMap, "MinStock"), 10),
                Supplier = GetField(fields, columnMap, "Supplier"),
                SupplierPartNumber = GetField(fields, columnMap, "SupplierPartNumber"),
                Value = GetField(fields, columnMap, "Value"),
                Package = GetField(fields, columnMap, "Package"),
                Tolerance = GetField(fields, columnMap, "Tolerance"),
                Voltage = GetField(fields, columnMap, "Voltage"),
                UnitPrice = ParseDecimal(GetField(fields, columnMap, "UnitPrice"), 0),
                Notes = GetField(fields, columnMap, "Notes"),
                DatasheetUrl = GetField(fields, columnMap, "DatasheetUrl"),
                SalesOrderNumber = GetField(fields, columnMap, "SalesOrderNumber")
            };
        }

        private async Task ValidateRow(
            BulkImportRow row,
            Dictionary<string, BoxRegistryItem> boxLookup,
            ImportLog importLog,
            string sourceFileName)
        {
            // 1. Skip if BoxName or Position is empty
            if (string.IsNullOrWhiteSpace(row.BoxName) || string.IsNullOrWhiteSpace(row.Position))
            {
                row.Status = ImportRowStatus.Skip;
                return;
            }

            // 3. Check if BoxName matches an existing box
            if (!boxLookup.TryGetValue(row.BoxName, out var boxItem))
            {
                row.Status = ImportRowStatus.InvalidBox;
                return;
            }

            row.BoxId = boxItem.Id;
            row.BoxType = boxItem.Type;

            // 4. Validate position against box type
            if (!PositionHelper.IsValidPosition(boxItem.Type, row.Position))
            {
                row.Status = ImportRowStatus.InvalidPosition;
                return;
            }

            // 5. Check import log for double-import
            if (_importLogService.IsAlreadyImported(importLog, row.PartNumber, row.Position, boxItem.Id))
            {
                row.Status = ImportRowStatus.AlreadyImported;
                return;
            }

            // 6. Check if compartment is already occupied
            try
            {
                var boxData = await _boxDataService.LoadBoxAsync(boxItem.Id);
                if (boxData != null)
                {
                    var compartment = boxData.Compartments.FirstOrDefault(
                        c => c.Position.Equals(row.Position, StringComparison.OrdinalIgnoreCase));

                    if (compartment?.Component != null &&
                        !string.IsNullOrEmpty(compartment.Component.PartNumber))
                    {
                        row.Status = ImportRowStatus.Conflict;
                        row.ExistingPartNumber = compartment.Component.PartNumber;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BulkCsvParser: Error checking compartment: {ex.Message}");
            }

            // 7. All checks passed
            row.Status = ImportRowStatus.Ready;
        }

        private Dictionary<string, int> BuildColumnMap(List<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i].Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    map[header] = i;
                }
            }
            return map;
        }

        private string GetField(List<string> fields, Dictionary<string, int> columnMap, string column, string defaultValue = "")
        {
            if (columnMap.TryGetValue(column, out int idx) && idx < fields.Count)
            {
                var val = fields[idx].Trim();
                return string.IsNullOrEmpty(val) ? defaultValue : val;
            }
            return defaultValue;
        }

        private int ParseInt(string value, int defaultValue)
        {
            if (int.TryParse(value.Replace(",", ""), out int result))
                return result;
            return defaultValue;
        }

        private decimal ParseDecimal(string value, decimal defaultValue)
        {
            var cleaned = value.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;
            return defaultValue;
        }

        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString().Trim());
            return fields;
        }
    }
}
