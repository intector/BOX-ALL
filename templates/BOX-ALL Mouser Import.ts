name: BOX-ALL Mouser Import
description: >-
  Transforms a Mouser order XLS into a "BOX-ALL Import" worksheet with validated
  dropdowns for BoxName, Position, and Category from boxall_status.json sync data.
  Tracks parts by SalesOrderNumber for re-open detection.
  Uses File System Access API to remember directories per button.
host: EXCEL
api_set: {}
script:
  content: >
    // ============================================================
    // BOX-ALL Mouser Import v4 – Script Lab Snippet
    // ============================================================
    // Integrates with boxall_status.json exported from the BOX-ALL app.
    // Tracks SalesOrderNumber per part for re-open detection.
    // Uses File System Access API so each button remembers its
    // last directory independently (id-based).
    //
    // Script Lab has 4 tabs. Copy each section into the matching tab.
    //   Tab 1: Script    → everything in "=== SCRIPT ===" section
    //   Tab 2: HTML      → everything in "=== HTML ===" section
    //   Tab 3: CSS       → everything in "=== CSS ===" section
    //   Tab 4: Libraries → everything in "=== LIBRARIES ===" section
    // ============================================================


    // =============================================================
    // === SCRIPT (paste into Script Lab "Script" tab) =============
    // =============================================================

    const BOXALL_COLUMNS = [
      "BoxName",
      "Position",
      "PartNumber",
      "Description",
      "Manufacturer",
      "Category",
      "Quantity",
      "MinStock",
      "Supplier",
      "SupplierPartNumber",
      "Value",
      "Package",
      "Tolerance",
      "Voltage",
      "UnitPrice",
      "Notes",
      "DatasheetUrl",
      "SalesOrderNumber",
    ];


    const MOUSER_HEADER_MAP: Record<string, string[]> = {
      SupplierPartNumber: ["Mouser #:", "Mouser #", "Mouser Part #"],
      PartNumber: ["Mfr. #:", "Mfr. #", "Mfr Part #", "Manufacturer Part Number"],
      Description: ["Desc.:", "Desc.", "Description"],
      Quantity: ["Order Qty.", "Order Qty", "Qty."],
      UnitPrice: ["Price (USD)", "Unit Price (USD)", "Price"],
    };


    // --- BOX-ALL Status data (loaded from JSON) ---

    interface SyncCompartment {
      position: string;
      partNumber: string;
      description: string;
      manufacturer: string;
      category: string;
      quantity: number;
      minStock: number;
      value: string;
      package: string;
      supplier: string;
      supplierPartNumber: string;
      unitPrice: number;
      notes: string;
      salesOrderNumber?: string;
    }

    interface BoxStatus {
      boxId: string;
      name: string;
      type: string;
      rows: number;
      columns: number;
      totalCompartments: number;
      occupiedCount: number;
      compartments: SyncCompartment[];
    }

    interface BoxAllStatus {
      exportDate: string;
      appVersion: string;
      boxes: BoxStatus[];
      categories: string[];
    }

    let loadedStatus: BoxAllStatus | null = null;
    let currentSalesOrderNumber: string = "";


    // --- Position generators (must match app's PositionHelper) ---

    function getPositions144(): string[] {
      const positions: string[] = [];
      for (let r = "A".charCodeAt(0); r <= "L".charCodeAt(0); r++) {
        for (let c = 1; c <= 12; c++) {
          positions.push(`${String.fromCharCode(r)}-${c.toString().padStart(2, "0")}`);
        }
      }
      return positions;
    }

    function getPositions96(): string[] {
      const positions: string[] = [];
      // Small rows A-F: 12 cols
      for (let r = "A".charCodeAt(0); r <= "F".charCodeAt(0); r++) {
        for (let c = 1; c <= 12; c++) {
          positions.push(`${String.fromCharCode(r)}-${c.toString().padStart(2, "0")}`);
        }
      }
      // Large rows G-H: 6 cols
      for (let r = "G".charCodeAt(0); r <= "H".charCodeAt(0); r++) {
        for (let c = 1; c <= 6; c++) {
          positions.push(`${String.fromCharCode(r)}-${c.toString().padStart(2, "0")}`);
        }
      }
      // Medium rows I-J: 6 cols
      for (let r = "I".charCodeAt(0); r <= "J".charCodeAt(0); r++) {
        for (let c = 1; c <= 6; c++) {
          positions.push(`${String.fromCharCode(r)}-${c.toString().padStart(2, "0")}`);
        }
      }
      return positions;
    }

    function getAllPositions(boxType: string): string[] {
      if (boxType && (boxType.includes("96") || boxType === "BOXALL96" || boxType === "BOXALL96AS")) {
        return getPositions96();
      }
      return getPositions144();
    }

    function getAvailablePositions(box: BoxStatus): string[] {
      const all = getAllPositions(box.type);
      const occupied = new Set(box.compartments.map((c) => c.position));
      return all.filter((p) => !occupied.has(p));
    }


    // --- Extract order number from filename ---

    function extractOrderNumber(filename: string): string {
      // "275422760.xls" → "275422760"
      const dotIdx = filename.lastIndexOf(".");
      return dotIdx >= 0 ? filename.substring(0, dotIdx) : filename;
    }


    // --- Build lookup: supplierPartNumber → full compartment data + boxName for a given order ---

    interface OrderMatch {
      boxName: string;
      comp: SyncCompartment;
    }

    function buildOrderLookup(
      status: BoxAllStatus,
      salesOrder: string
    ): Map<string, OrderMatch> {
      const lookup = new Map<string, OrderMatch>();
      for (const box of status.boxes) {
        for (const comp of box.compartments) {
          if (
            comp.salesOrderNumber &&
            comp.salesOrderNumber === salesOrder &&
            comp.supplierPartNumber
          ) {
            lookup.set(comp.supplierPartNumber, {
              boxName: box.name,
              comp: comp,
            });
          }
        }
      }
      return lookup;
    }


    function cleanDescription(desc: string): string {
      if (!desc || desc.length < 10) return desc;
      const half = Math.floor(desc.length / 2);
      for (let len = half; len >= 5; len--) {
        const prefix = desc.substring(0, len);
        const rest = desc.substring(len);
        if (rest.startsWith(prefix)) return rest.trim();
        const tp = prefix.trimEnd();
        const rt = rest.trimStart();
        if (rt.startsWith(tp)) return rt.trim();
      }
      return desc;
    }


    function setStatus(msg: string, type: "info" | "success" | "error" = "info") {
      const el = document.getElementById("status");
      el.textContent = msg;
      el.className = "status " + type;
    }


    // --- CSV helper: escape a field for CSV ---

    function csvEscape(val: any): string {
      if (val === null || val === undefined) return "";
      const str = String(val);
      if (str.includes(",") || str.includes('"') || str.includes("\n") || str.includes("\r")) {
        return '"' + str.replace(/"/g, '""') + '"';
      }
      return str;
    }


    // --- Check if File System Access API is available ---

    function hasFileSystemAccess(): boolean {
      return typeof (window as any).showOpenFilePicker === "function";
    }


    // --- Load BOX-ALL Status JSON ---

    document.getElementById("loadStatusBtn").addEventListener("click", async () => {
      try {
        let file: File;

        if (hasFileSystemAccess()) {
          const [handle] = await (window as any).showOpenFilePicker({
            id: "boxall-status",
            types: [{ description: "JSON files", accept: { "application/json": [".json"] } }],
          });
          file = await handle.getFile();
        } else {
          // Fallback: hidden input
          file = await new Promise<File>((resolve, reject) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = ".json";
            input.onchange = () => input.files?.length ? resolve(input.files[0]) : reject(new Error("No file selected"));
            input.click();
          });
        }

        setStatus(`Loading "${file.name}"...`);

        const text = await file.text();
        loadedStatus = JSON.parse(text) as BoxAllStatus;

        if (!loadedStatus.boxes || !loadedStatus.boxes.length) {
          setStatus("No boxes found in status file.", "error");
          loadedStatus = null;
          return;
        }

        // Build summary
        const boxCount = loadedStatus.boxes.length;
        const totalOccupied = loadedStatus.boxes.reduce((sum, b) => sum + b.occupiedCount, 0);
        const totalCompartments = loadedStatus.boxes.reduce((sum, b) => sum + b.totalCompartments, 0);
        const exportDate = new Date(loadedStatus.exportDate).toLocaleString();

        // Show box list
        const boxListEl = document.getElementById("boxList");
        boxListEl.innerHTML = loadedStatus.boxes
          .map(
            (b) =>
              `<div class="box-item">
                <strong>${b.name}</strong>
                <span class="box-detail">${b.type} – ${b.occupiedCount}/${b.totalCompartments} used</span>
              </div>`
          )
          .join("");

        document.getElementById("statusInfo").style.display = "block";
        document.getElementById("statusDate").textContent = exportDate;

        // Enable Mouser import section
        document.getElementById("importSection").style.display = "block";

        setStatus(
          `✅ Loaded ${boxCount} box(es): ${totalOccupied}/${totalCompartments} compartments occupied`,
          "success"
        );
      } catch (err) {
        if (err.name === "AbortError") return; // user cancelled picker
        setStatus(`Error reading status file: ${err.message || err}`, "error");
        loadedStatus = null;
      }
    });


    // --- Handle Mouser file selection ---

    document.getElementById("loadMouserBtn").addEventListener("click", async () => {
      try {
        let file: File;

        if (hasFileSystemAccess()) {
          const [handle] = await (window as any).showOpenFilePicker({
            id: "mouser-order",
            types: [{ description: "Excel/CSV files", accept: {
              "application/vnd.ms-excel": [".xls"],
              "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
              "text/csv": [".csv"],
            }}],
          });
          file = await handle.getFile();
        } else {
          file = await new Promise<File>((resolve, reject) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = ".xls,.xlsx,.csv";
            input.onchange = () => input.files?.length ? resolve(input.files[0]) : reject(new Error("No file selected"));
            input.click();
          });
        }

        const salesOrderNumber = extractOrderNumber(file.name);
        currentSalesOrderNumber = salesOrderNumber;
        setStatus(`Reading "${file.name}" (Order #${salesOrderNumber})...`);

        // Build lookup for already-imported parts from this order
        const orderLookup = loadedStatus
          ? buildOrderLookup(loadedStatus, salesOrderNumber)
          : new Map<string, OrderMatch>();

        const data = await file.arrayBuffer();
        const XLSX = (window as any).XLSX;
        const wb = XLSX.read(data, { type: "array" });

        // Use first sheet
        const sheetName = wb.SheetNames[0];
        const ws = wb.Sheets[sheetName];
        const rows: any[][] = XLSX.utils.sheet_to_json(ws, { header: 1, defval: "" });

        if (rows.length < 2) {
          setStatus("No data rows found in file.", "error");
          return;
        }

        const sourceHeaders: string[] = rows[0].map((h: any) => String(h).trim());
        const dataRows = rows
          .slice(1)
          .filter((r: any[]) =>
            r.some((cell: any) => cell !== null && cell !== undefined && String(cell).trim() !== "")
          );

        setStatus(`Parsed ${dataRows.length} rows from "${sheetName}". Writing to Excel...`);

        // Map Mouser columns
        const colIndex: Record<string, number> = {};
        for (const [field, variants] of Object.entries(MOUSER_HEADER_MAP)) {
          for (const v of variants) {
            const idx = sourceHeaders.findIndex((h) => h.toLowerCase() === v.toLowerCase());
            if (idx >= 0) {
              colIndex[field] = idx;
              break;
            }
          }
        }

        // Build output rows – track which rows are already imported
        const outputData: any[][] = [];
        const alreadyImportedRows: number[] = []; // 0-based indices into outputData

        for (const row of dataRows) {
          const mouserNum =
            colIndex.SupplierPartNumber !== undefined ? String(row[colIndex.SupplierPartNumber]).trim() : "";
          if (!mouserNum) continue;

          // Check if this part was already imported from this order
          const existing = orderLookup.get(mouserNum);

          if (existing) {
            // Use values from the sync file – these are the authoritative source
            const c = existing.comp;
            alreadyImportedRows.push(outputData.length);
            outputData.push([
              existing.boxName,
              c.position,
              c.partNumber,
              c.description,
              c.manufacturer,
              c.category || "Other",
              c.quantity,
              c.minStock,
              c.supplier,
              c.supplierPartNumber,
              c.value,
              c.package,
              "", // Tolerance (not in sync file)
              "", // Voltage (not in sync file)
              c.unitPrice,
              c.notes,
              "", // DatasheetUrl (not in sync file)
              salesOrderNumber,
            ]);
          } else {
            // New part – use values from Mouser file
            const mfrNum = colIndex.PartNumber !== undefined ? String(row[colIndex.PartNumber]).trim() : "";

            let desc = colIndex.Description !== undefined ? String(row[colIndex.Description]).trim() : "";
            desc = cleanDescription(desc);

            let qty = colIndex.Quantity !== undefined ? row[colIndex.Quantity] : 0;
            qty = parseInt(String(qty).replace(/[^0-9]/g, "")) || 0;

            let price = colIndex.UnitPrice !== undefined ? row[colIndex.UnitPrice] : 0;
            price = parseFloat(String(price).replace(/[$,]/g, "")) || 0;

            outputData.push([
              "", // BoxName – user fills via dropdown
              "", // Position – user fills via dropdown
              mfrNum,
              desc,
              "", // Manufacturer
              "Other", // Category – default
              qty,
              10, // MinStock default
              "Mouser",
              mouserNum,
              "", // Value
              "", // Package
              "", // Tolerance
              "", // Voltage
              price,
              "", // Notes
              "", // DatasheetUrl
              salesOrderNumber,
            ]);
          }
        }

        if (outputData.length === 0) {
          setStatus("No valid data rows after parsing.", "error");
          return;
        }

        // Write to Excel
        await Excel.run(async (context) => {
          // Delete existing sheets if present
          try {
            const existing = context.workbook.worksheets.getItemOrNullObject("BOX-ALL Import");
            existing.load("isNullObject");
            await context.sync();
            if (!existing.isNullObject) {
              existing.delete();
              await context.sync();
            }
          } catch {}

          const helperSheetName = "_BOX-ALL_Helpers";
          try {
            const existingHelper = context.workbook.worksheets.getItemOrNullObject(helperSheetName);
            existingHelper.load("isNullObject");
            await context.sync();
            if (!existingHelper.isNullObject) {
              existingHelper.delete();
              await context.sync();
            }
          } catch {}

          const sheet = context.workbook.worksheets.add("BOX-ALL Import");

          // --- Headers ---
          const headerRange = sheet.getRangeByIndexes(0, 0, 1, BOXALL_COLUMNS.length);
          headerRange.values = [BOXALL_COLUMNS];
          headerRange.format.font.bold = true;
          headerRange.format.fill.color = "#D9E1F2";
          headerRange.format.horizontalAlignment = Excel.HorizontalAlignment.center;

          // Yellow highlight on BoxName + Position + Category headers
          sheet.getRangeByIndexes(0, 0, 1, 1).format.fill.color = "#FFD966"; // BoxName
          sheet.getRangeByIndexes(0, 1, 1, 1).format.fill.color = "#FFD966"; // Position
          sheet.getRangeByIndexes(0, 5, 1, 1).format.fill.color = "#FFD966"; // Category

          // --- Data ---
          const dataRange = sheet.getRangeByIndexes(1, 0, outputData.length, BOXALL_COLUMNS.length);
          dataRange.values = outputData;

          // Default yellow highlight on BoxName + Position data columns
          sheet.getRangeByIndexes(1, 0, outputData.length, 1).format.fill.color = "#FFF2CC";
          sheet.getRangeByIndexes(1, 1, outputData.length, 1).format.fill.color = "#FFF2CC";

          // --- Already-imported rows: green + locked ---
          for (const rowIdx of alreadyImportedRows) {
            const excelRow = rowIdx + 1; // +1 for header
            const boxCell = sheet.getRangeByIndexes(excelRow, 0, 1, 1);
            const posCell = sheet.getRangeByIndexes(excelRow, 1, 1, 1);
            boxCell.format.fill.color = "#DEF7EC"; // green
            posCell.format.fill.color = "#DEF7EC";
            boxCell.format.font.italic = true;
            posCell.format.font.italic = true;

            // Remove dropdown validation for already-imported rows
            // (they keep their pre-filled values but can't be changed via dropdown)
            boxCell.dataValidation.clear();
            posCell.dataValidation.clear();
          }

          // --- Data Validation Dropdowns (if status loaded) ---
          if (loadedStatus) {
            const helperSheet = context.workbook.worksheets.add(helperSheetName);
            helperSheet.visibility = Excel.SheetVisibility.hidden;

            // All positions across all box types
            const allPositions = new Set<string>();
            for (const box of loadedStatus.boxes) {
              for (const pos of getAllPositions(box.type)) {
                allPositions.add(pos);
              }
            }
            const sortedPositions = Array.from(allPositions).sort();

            // Write to helper sheet: A=positions, B=box names, C=categories
            const posValues = sortedPositions.map((p) => [p]);
            helperSheet.getRangeByIndexes(0, 0, posValues.length, 1).values = posValues;

            const boxNames = loadedStatus.boxes.map((b) => b.name).sort();
            const boxNameValues = boxNames.map((n) => [n]);
            helperSheet.getRangeByIndexes(0, 1, boxNameValues.length, 1).values = boxNameValues;

            const categories = loadedStatus.categories;
            const catValues = categories.map((c) => [c]);
            helperSheet.getRangeByIndexes(0, 2, catValues.length, 1).values = catValues;

            await context.sync();

            // Apply dropdowns
            const importedSet = new Set(alreadyImportedRows);

            for (let i = 0; i < outputData.length; i++) {
              const excelRow = i + 1;

              // Category dropdown – all rows (including already-imported)
              const catCell = sheet.getRangeByIndexes(excelRow, 5, 1, 1);
              catCell.dataValidation.rule = {
                list: {
                  inCellDropDown: true,
                  source: `='${helperSheetName}'!$C$1:$C$${catValues.length}`,
                },
              };

              if (importedSet.has(i)) continue; // BoxName/Position only for new rows

              // BoxName dropdown
              const bnCell = sheet.getRangeByIndexes(excelRow, 0, 1, 1);
              bnCell.dataValidation.rule = {
                list: {
                  inCellDropDown: true,
                  source: `='${helperSheetName}'!$B$1:$B$${boxNameValues.length}`,
                },
              };

              // Position dropdown
              const posCell = sheet.getRangeByIndexes(excelRow, 1, 1, 1);
              posCell.dataValidation.rule = {
                list: {
                  inCellDropDown: true,
                  source: `='${helperSheetName}'!$A$1:$A$${posValues.length}`,
                },
              };
            }
          }

          // Format UnitPrice as currency
          const priceIdx = BOXALL_COLUMNS.indexOf("UnitPrice");
          sheet.getRangeByIndexes(1, priceIdx, outputData.length, 1).numberFormat = [["$#,##0.000"]];

          // SalesOrderNumber column – light gray, read-only appearance
          const sonIdx = BOXALL_COLUMNS.indexOf("SalesOrderNumber");
          sheet.getRangeByIndexes(1, sonIdx, outputData.length, 1).format.fill.color = "#E5E7EB";
          sheet.getRangeByIndexes(1, sonIdx, outputData.length, 1).format.font.color = "#6B7280";

          // Auto-fit and freeze
          sheet.getRangeByIndexes(0, 0, outputData.length + 1, BOXALL_COLUMNS.length).format.autofitColumns();
          sheet.freezePanes.freezeRows(1);

          sheet.activate();
          await context.sync();
        });

        // Build suggested filename
        const today = new Date().toISOString().split("T")[0];
        const suggestedName = `${today}_mouser_${salesOrderNumber}.csv`;

        document.getElementById("nextSteps").style.display = "block";
        document.getElementById("suggestedFilename").textContent = suggestedName;

        const importedCount = alreadyImportedRows.length;
        const newCount = outputData.length - importedCount;
        let msg = `✅ Created "BOX-ALL Import" – ${outputData.length} rows from order #${salesOrderNumber}`;
        if (importedCount > 0) {
          msg += ` (${importedCount} already assigned, ${newCount} new)`;
        }
        setStatus(msg, "success");
      } catch (err) {
        if (err.name === "AbortError") return; // user cancelled picker
        setStatus(`Error: ${err.message || err}`, "error");
      }
    });


    // --- Save as CSV Button ---

    document.getElementById("saveCsvBtn").addEventListener("click", async () => {
      try {
        setStatus("Reading sheet data...");

        let csvContent = "";

        await Excel.run(async (context) => {
          const sheet = context.workbook.worksheets.getItemOrNullObject("BOX-ALL Import");
          sheet.load("isNullObject");
          await context.sync();

          if (sheet.isNullObject) {
            setStatus('No "BOX-ALL Import" sheet found.', "error");
            return;
          }

          const usedRange = sheet.getUsedRange();
          usedRange.load("values");
          await context.sync();

          const allValues = usedRange.values;

          // Build CSV string
          const lines: string[] = [];
          for (const row of allValues) {
            lines.push(row.map((cell: any) => csvEscape(cell)).join(","));
          }
          csvContent = lines.join("\r\n") + "\r\n";
        });

        if (!csvContent) return;

        // Build suggested filename
        const today = new Date().toISOString().split("T")[0];
        const suggestedName = currentSalesOrderNumber
          ? `${today}_mouser_${currentSalesOrderNumber}.csv`
          : `${today}_boxall_import.csv`;

        if (hasFileSystemAccess()) {
          const handle = await (window as any).showSaveFilePicker({
            id: "boxall-csv",
            suggestedName: suggestedName,
            types: [{ description: "CSV files", accept: { "text/csv": [".csv"] } }],
          });
          const writable = await handle.createWritable();
          await writable.write(csvContent);
          await writable.close();
          setStatus(`✅ Saved: ${handle.name}`, "success");
        } else {
          // Fallback: trigger download via blob
          const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
          const url = URL.createObjectURL(blob);
          const a = document.createElement("a");
          a.href = url;
          a.download = suggestedName;
          a.click();
          URL.revokeObjectURL(url);
          setStatus(`✅ Downloaded: ${suggestedName}`, "success");
        }
      } catch (err) {
        if (err.name === "AbortError") return; // user cancelled picker
        setStatus(`Save error: ${err.message || err}`, "error");
      }
    });


    // --- Validate Button ---

    document.getElementById("validateBtn").addEventListener("click", async () => {
      if (!loadedStatus) {
        setStatus("Load a BOX-ALL status file first.", "error");
        return;
      }

      setStatus("Validating...");

      try {
        await Excel.run(async (context) => {
          const sheet = context.workbook.worksheets.getItemOrNullObject("BOX-ALL Import");
          sheet.load("isNullObject");
          await context.sync();

          if (sheet.isNullObject) {
            setStatus('No "BOX-ALL Import" sheet found. Import a Mouser file first.', "error");
            return;
          }

          const usedRange = sheet.getUsedRange();
          usedRange.load("rowCount, columnCount, values");
          await context.sync();

          const allValues = usedRange.values;
          const rowCount = allValues.length - 1; // exclude header
          if (rowCount < 1) {
            setStatus("No data rows to validate.", "error");
            return;
          }

          // Find SalesOrderNumber column index
          const headers = allValues[0].map((h: any) => String(h).trim());
          const sonColIdx = headers.indexOf("SalesOrderNumber");

          // Build lookup: boxName → BoxStatus
          const boxByName = new Map<string, BoxStatus>();
          for (const box of loadedStatus.boxes) {
            boxByName.set(box.name.toLowerCase(), box);
          }

          // Build order lookup for already-imported detection
          let orderLookup = new Map<string, { boxName: string; position: string }>();
          if (sonColIdx >= 0) {
            const firstSalesOrder = String(allValues[1][sonColIdx] || "").trim();
            if (firstSalesOrder) {
              orderLookup = buildOrderLookup(loadedStatus, firstSalesOrder);
            }
          }

          let readyCount = 0;
          let skipCount = 0;
          let conflictCount = 0;
          let importedCount = 0;

          // Track positions assigned in THIS import to detect duplicates within the sheet
          const assignedInSheet = new Map<string, Set<string>>();

          for (let i = 1; i <= rowCount; i++) {
            let boxName = String(allValues[i][0] || "").trim();
            let position = String(allValues[i][1] || "").trim();
            const supplierPN = String(allValues[i][9] || "").trim(); // SupplierPartNumber col

            const rowBoxCell = sheet.getRangeByIndexes(i, 0, 1, 1);
            const rowPosCell = sheet.getRangeByIndexes(i, 1, 1, 1);

            // Check if this part exists in the system from this order
            const existing = orderLookup.get(supplierPN);
            if (existing) {
              // Silently correct BoxName/Position to match the system
              rowBoxCell.values = [[existing.boxName]];
              rowPosCell.values = [[existing.comp.position]];
              rowBoxCell.format.fill.color = "#DEF7EC"; // green – already in system
              rowPosCell.format.fill.color = "#DEF7EC";
              rowBoxCell.format.font.italic = true;
              rowPosCell.format.font.italic = true;
              importedCount++;
              continue;
            }

            if (!boxName || !position) {
              rowBoxCell.format.fill.color = "#FFF2CC"; // yellow – not filled
              rowPosCell.format.fill.color = "#FFF2CC";
              skipCount++;
              continue;
            }

            // Look up box
            const box = boxByName.get(boxName.toLowerCase());
            if (!box) {
              skipCount++;
              continue;
            }

            // Check if position already occupied in app
            const isOccupied = box.compartments.some((c) => c.position === position);

            // Check if position already used earlier in this sheet
            const sheetKey = boxName.toLowerCase();
            if (!assignedInSheet.has(sheetKey)) {
              assignedInSheet.set(sheetKey, new Set());
            }
            const isDuplicateInSheet = assignedInSheet.get(sheetKey).has(position);
            assignedInSheet.get(sheetKey).add(position);

            if (isOccupied || isDuplicateInSheet) {
              rowBoxCell.format.fill.color = "#FDE8E8"; // red – conflict
              rowPosCell.format.fill.color = "#FDE8E8";
              conflictCount++;
            } else {
              rowBoxCell.format.fill.color = "#DEF7EC"; // green – ready
              rowPosCell.format.fill.color = "#DEF7EC";
              readyCount++;
            }
          }

          await context.sync();

          // Build result message
          const parts: string[] = [];
          if (importedCount > 0) parts.push(`📱 ${importedCount} already assigned`);
          if (readyCount > 0) parts.push(`✅ ${readyCount} ready`);
          if (conflictCount > 0) parts.push(`❌ ${conflictCount} conflict(s)`);
          if (skipCount > 0) parts.push(`⭕ ${skipCount} not filled`);

          const resultType = conflictCount > 0 ? "error" : "success";
          setStatus(parts.join(" · "), resultType);

          // Show legend
          document.getElementById("legend").style.display = "block";
        });
      } catch (err) {
        setStatus(`Validation error: ${err.message || err}`, "error");
      }
    });
  language: typescript
template:
  content: |
    <main>
      <h2>BOX-ALL Mouser Import</h2>

      <div class="section">
        <h3>Step 1: Load BOX-ALL Status</h3>
        <p>Load the <code>boxall_status.json</code> exported from the app's Sync function.</p>
        <button id="loadStatusBtn" class="file-label status-label">
          📱 Load boxall_status.json
        </button>

        <div id="statusInfo" style="display:none">
          <div class="info-badge">Synced: <span id="statusDate"></span></div>
          <div id="boxList" class="box-list"></div>
        </div>
      </div>

      <div id="importSection" class="section" style="display:none">
        <h3>Step 2: Import Mouser Order</h3>
        <p>Select a Mouser order file (.xls or .xlsx). Parts already assigned from this order will be pre-filled.</p>
        <button id="loadMouserBtn" class="file-label">
          📂 Select Mouser Order File
        </button>
      </div>

      <div id="status" class="status"></div>

      <div id="nextSteps" style="display:none">
        <h3>Next Steps</h3>
        <ol>
          <li>Fill in <strong>BoxName</strong> and <strong>Position</strong> using the dropdowns (yellow columns)</li>
          <li>Green rows are already assigned – no changes needed</li>
          <li>Optionally change <strong>Category</strong> and fill Manufacturer, Value, Package, etc.</li>
          <li>Click <strong>Validate</strong> to check for conflicts</li>
          <li>Click <strong>Save as CSV</strong> to export:<br/>
            <code id="suggestedFilename"></code></li>
          <li>Transfer CSV to phone → BOX-ALL app → Bulk Import</li>
        </ol>

        <div class="btn-row">
          <button id="validateBtn" class="validate-btn">🔍 Validate</button>
          <button id="saveCsvBtn" class="save-btn">💾 Save as CSV</button>
        </div>

        <div id="legend" class="legend" style="display:none">
          <div><span class="dot green"></span> Ready / Already assigned</div>
          <div><span class="dot red"></span> Conflict (occupied or duplicate)</div>
          <div><span class="dot yellow"></span> Not filled in yet</div>
        </div>
      </div>
    </main>
  language: html
style:
  content: |
    main {
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      padding: 16px;
      max-width: 420px;
    }

    h2 {
      color: #0A0E27;
      margin-bottom: 4px;
      font-size: 18px;
    }

    h3 {
      color: #0A0E27;
      font-size: 14px;
      margin: 0 0 4px 0;
    }

    p {
      color: #555;
      font-size: 13px;
      margin-bottom: 12px;
    }

    .section {
      margin-bottom: 20px;
      padding: 12px;
      background: #f8f9fa;
      border-radius: 6px;
      border: 1px solid #dee2e6;
    }

    .file-label {
      display: inline-block;
      padding: 10px 20px;
      background: #0A0E27;
      color: white;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      font-weight: 600;
      transition: background 0.2s;
    }

    .file-label:hover {
      background: #1a2047;
    }

    .status-label {
      background: #1a56db;
    }

    .status-label:hover {
      background: #1e40af;
    }

    .status {
      margin-top: 12px;
      padding: 10px 12px;
      border-radius: 4px;
      font-size: 13px;
      min-height: 20px;
    }

    .status.info {
      background: #E8F0FE;
      color: #1a56db;
    }

    .status.success {
      background: #DEF7EC;
      color: #046c4e;
    }

    .status.error {
      background: #FDE8E8;
      color: #c81e1e;
    }

    .info-badge {
      margin-top: 8px;
      font-size: 12px;
      color: #666;
    }

    .box-list {
      margin-top: 8px;
    }

    .box-item {
      padding: 6px 8px;
      margin: 4px 0;
      background: white;
      border-radius: 4px;
      border: 1px solid #e5e7eb;
      font-size: 13px;
    }

    .box-item strong {
      color: #0A0E27;
    }

    .box-detail {
      float: right;
      color: #6b7280;
      font-size: 12px;
    }

    #nextSteps {
      margin-top: 16px;
      padding: 12px;
      background: #f8f9fa;
      border-radius: 6px;
      border: 1px solid #dee2e6;
    }

    #nextSteps h3 {
      margin: 0 0 8px 0;
      font-size: 14px;
      color: #0A0E27;
    }

    #nextSteps ol {
      padding-left: 20px;
      margin: 0 0 12px 0;
      font-size: 13px;
      line-height: 1.6;
    }

    #nextSteps code {
      background: #FFD966;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 12px;
    }

    .btn-row {
      display: flex;
      gap: 10px;
      margin-bottom: 8px;
    }

    .validate-btn, .save-btn {
      display: inline-block;
      padding: 10px 20px;
      color: white;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      font-weight: 600;
      transition: background 0.2s;
    }

    .validate-btn {
      background: #0A0E27;
    }

    .validate-btn:hover {
      background: #1a2047;
    }

    .save-btn {
      background: #046c4e;
    }

    .save-btn:hover {
      background: #035c42;
    }

    .legend {
      margin-top: 12px;
      font-size: 12px;
      color: #555;
    }

    .legend div {
      margin: 4px 0;
    }

    .dot {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 3px;
      margin-right: 6px;
      vertical-align: middle;
    }

    .dot.green { background: #DEF7EC; border: 1px solid #046c4e; }
    .dot.red { background: #FDE8E8; border: 1px solid #c81e1e; }
    .dot.yellow { background: #FFF2CC; border: 1px solid #d69e2e; }
  language: css
libraries: >-
  https://appsforoffice.microsoft.com/lib/1/hosted/office.js

  https://raw.githubusercontent.com/DefinitelyTyped/DefinitelyTyped/master/types/office-js/index.d.ts

  https://cdn.sheetjs.com/xlsx-0.20.3/package/dist/xlsx.full.min.js
  