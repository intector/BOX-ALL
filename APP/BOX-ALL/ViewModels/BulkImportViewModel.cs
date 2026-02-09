using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BOX_ALL.Models;
using BOX_ALL.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BOX_ALL.ViewModels
{
    public partial class BulkImportViewModel : ObservableObject
    {
        private readonly BulkCsvParserService _parser;
        private readonly BoxDataService _boxDataService;
        private readonly ImportLogService _importLogService;
        private string _sourceFileName = "";

        [ObservableProperty]
        private ObservableCollection<BulkImportRow> rows = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = "";

        [ObservableProperty]
        private string pageTitle = "Bulk Import";

        [ObservableProperty]
        private bool hasRows;

        [ObservableProperty]
        private bool canImport;

        [ObservableProperty]
        private bool showSummary;

        [ObservableProperty]
        private string summaryText = "";

        // Counts for preview
        [ObservableProperty]
        private int readyCount;

        [ObservableProperty]
        private int conflictCount;

        [ObservableProperty]
        private int skipCount;

        [ObservableProperty]
        private int errorCount;

        public BulkImportViewModel(
            BulkCsvParserService parser,
            BoxDataService boxDataService,
            ImportLogService importLogService)
        {
            _parser = parser;
            _boxDataService = boxDataService;
            _importLogService = importLogService;
        }

        public async Task LoadCsvFileAsync(Stream stream, string fileName)
        {
            IsBusy = true;
            StatusMessage = "Parsing CSV...";
            _sourceFileName = fileName;

            try
            {
                var parsed = await _parser.ParseAndValidateAsync(stream, fileName);

                Rows.Clear();
                foreach (var row in parsed)
                {
                    Rows.Add(row);
                }

                UpdateCounts();
                HasRows = Rows.Count > 0;
                CanImport = ReadyCount > 0 || ConflictCount > 0;
                ShowSummary = false;

                StatusMessage = $"Loaded {Rows.Count} rows from {fileName}";
                PageTitle = $"Bulk Import ({Rows.Count})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error parsing CSV: {ex.Message}";
                Debug.WriteLine($"BulkImportVM: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateCounts()
        {
            ReadyCount = Rows.Count(r => r.Status == ImportRowStatus.Ready);
            ConflictCount = Rows.Count(r => r.Status == ImportRowStatus.Conflict);
            SkipCount = Rows.Count(r =>
                r.Status == ImportRowStatus.Skip ||
                r.Status == ImportRowStatus.AlreadyImported);
            ErrorCount = Rows.Count(r =>
                r.Status == ImportRowStatus.InvalidBox ||
                r.Status == ImportRowStatus.InvalidPosition);
        }

        [RelayCommand]
        private async Task ImportAll()
        {
            if (!CanImport) return;

            IsBusy = true;
            int imported = 0;
            int skipped = 0;
            int overwritten = 0;
            int failed = 0;

            try
            {
                // Process conflicts first — ask once
                var conflicts = Rows.Where(r => r.Status == ImportRowStatus.Conflict).ToList();
                bool overwriteAll = false;

                if (conflicts.Count > 0)
                {
                    overwriteAll = await ShowConfirm(
                        "Overwrite Conflicts?",
                        $"{conflicts.Count} compartment(s) are already occupied.\n\nOverwrite all with new data?");

                    if (!overwriteAll)
                    {
                        foreach (var c in conflicts)
                        {
                            c.Status = ImportRowStatus.Skipped;
                            skipped++;
                        }
                    }
                }

                // Import ready rows + (conflicts if overwrite approved)
                var toImport = Rows.Where(r =>
                    r.Status == ImportRowStatus.Ready ||
                    (r.Status == ImportRowStatus.Conflict && overwriteAll)).ToList();

                StatusMessage = $"Importing {toImport.Count} components...";

                foreach (var row in toImport)
                {
                    try
                    {
                        bool wasOverwrite = row.Status == ImportRowStatus.Conflict;

                        // Save component to box
                        var componentData = row.ToComponentData();
                        await _boxDataService.AddComponentAsync(row.BoxId!, row.Position, componentData);

                        // Log the import
                        await _importLogService.AddEntryAsync(new ImportLogEntry
                        {
                            PartNumber = row.PartNumber,
                            SupplierPartNumber = row.SupplierPartNumber,
                            BoxName = row.BoxName,
                            BoxId = row.BoxId!,
                            Position = row.Position,
                            Quantity = row.Quantity,
                            ImportDate = DateTime.Now,
                            SourceFile = _sourceFileName,
                            Overwritten = wasOverwrite
                        });

                        row.Status = ImportRowStatus.Imported;
                        imported++;
                        if (wasOverwrite) overwritten++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"BulkImportVM: Failed to import row {row.RowNumber}: {ex.Message}");
                        failed++;
                    }
                }

                // Count remaining skips
                skipped += Rows.Count(r =>
                    r.Status == ImportRowStatus.Skip ||
                    r.Status == ImportRowStatus.AlreadyImported ||
                    r.Status == ImportRowStatus.InvalidBox ||
                    r.Status == ImportRowStatus.InvalidPosition);

                // Show summary
                var summary = $"✅ {imported} imported";
                if (overwritten > 0) summary += $" ({overwritten} overwritten)";
                if (skipped > 0) summary += $"\n⭐ {skipped} skipped";
                if (failed > 0) summary += $"\n❌ {failed} failed";

                SummaryText = summary;
                ShowSummary = true;
                CanImport = false;
                StatusMessage = "Import complete";

                await ShowAlert("Import Complete", summary);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import error: {ex.Message}";
                Debug.WriteLine($"BulkImportVM: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task ShowAlert(string title, string message)
        {
            if (Application.Current?.Windows?.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "OK");
            }
        }

        private async Task<bool> ShowConfirm(string title, string message)
        {
            if (Application.Current?.Windows?.Count > 0)
            {
                return await Application.Current.Windows[0].Page!.DisplayAlert(title, message, "Yes", "No");
            }
            return false;
        }
    }
}
