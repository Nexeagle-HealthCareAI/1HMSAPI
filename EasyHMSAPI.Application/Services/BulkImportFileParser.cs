using ClosedXML.Excel;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Reads a distributor's stock-intake spreadsheet (.xlsx or .csv) into raw string rows keyed by
    /// a normalized header name — "Exp Date"/"Expiry"/"Val Date" all normalize to "EXPIRYDATE", so
    /// the caller doesn't have to know which exact header text a given distributor used. Column
    /// VALUES aren't interpreted here (dates/numbers stay strings) — that's PreviewBulkImportHandler's
    /// job, since only it knows what "invalid" means for a given field.
    /// </summary>
    public static class BulkImportFileParser
    {
        // Canonical field -> every header spelling seen in the wild that should map to it.
        private static readonly Dictionary<string, string[]> HeaderAliases = new()
        {
            ["STORECODE"] = new[] { "STORE", "STORECODE", "STORE CODE", "LOCATION" },
            ["ITEMCODE"] = new[] { "ITEM", "ITEMCODE", "ITEM CODE", "CODE", "PRODUCT CODE", "SKU" },
            // Optional -- only needed when ITEMCODE doesn't already exist in the catalogue, in which
            // case the commit step auto-creates a new medicine from this name instead of rejecting
            // the row (see BulkBatchCommandHandlers). One-step "add medicine + stock it" workflow.
            ["ITEMNAME"] = new[] { "ITEM NAME", "ITEMNAME", "NAME", "DRUG NAME", "MEDICINE NAME", "PRODUCT NAME" },
            ["BATCHNUMBER"] = new[] { "BATCH", "BATCHNO", "BATCH NO", "BATCH NUMBER", "BATCHNUMBER", "LOT", "LOT NO" },
            ["MANUFACTUREDATE"] = new[] { "MFG DATE", "MFG", "MANUFACTURE DATE", "MANUFACTUREDATE", "MFD" },
            ["EXPIRYDATE"] = new[] { "EXP DATE", "EXPIRY", "EXPIRY DATE", "EXPIRYDATE", "VAL DATE", "VALIDITY", "EXP" },
            ["RECEIVEDQTY"] = new[] { "QTY", "QUANTITY", "RECEIVED QTY", "RECEIVEDQTY", "BILLED QTY" },
            ["UNITCOST"] = new[] { "RATE", "COST", "UNIT COST", "UNITCOST", "PURCHASE RATE", "PRICE" },
            ["MRP"] = new[] { "MRP", "MAX RETAIL PRICE", "PRINTED PRICE" },
            ["BARCODEVALUE"] = new[] { "BARCODE", "BARCODEVALUE", "BAR CODE", "EAN" },
        };

        public static string NormalizeHeader(string? raw) =>
            (raw ?? string.Empty).Trim().ToUpperInvariant().Replace(".", "").Replace("_", " ").Replace("-", " ");

        /// <summary>Maps a normalized/raw header cell to the canonical field name, or null if unrecognized.</summary>
        public static string? ResolveCanonicalField(string? rawHeader)
        {
            var normalized = NormalizeHeader(rawHeader);
            foreach (var (canonical, aliases) in HeaderAliases)
            {
                if (aliases.Any(a => NormalizeHeader(a) == normalized))
                    return canonical;
            }
            return null;
        }

        /// <summary>Each row is canonical-field -> raw cell text (unmapped columns are dropped).</summary>
        public static List<Dictionary<string, string>> Parse(Stream fileStream, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".csv" => ParseCsv(fileStream),
                ".xlsx" or ".xls" => ParseXlsx(fileStream),
                _ => throw new InvalidOperationException("Unsupported file type — upload a .csv or .xlsx file."),
            };
        }

        private static List<Dictionary<string, string>> ParseXlsx(Stream fileStream)
        {
            using var workbook = new XLWorkbook(fileStream);
            var sheet = workbook.Worksheets.First();
            var usedRange = sheet.RangeUsed();
            if (usedRange == null) return new List<Dictionary<string, string>>();

            var rows = usedRange.RowsUsed().ToList();
            if (rows.Count == 0) return new List<Dictionary<string, string>>();

            var headerRow = rows[0];
            var columnMap = new Dictionary<int, string>(); // column index -> canonical field
            foreach (var cell in headerRow.CellsUsed())
            {
                var canonical = ResolveCanonicalField(cell.GetString());
                if (canonical != null) columnMap[cell.Address.ColumnNumber] = canonical;
            }

            var result = new List<Dictionary<string, string>>();
            for (int r = 1; r < rows.Count; r++)
            {
                var dataRow = new Dictionary<string, string>();
                foreach (var (colIndex, canonical) in columnMap)
                {
                    var cell = rows[r].Worksheet.Cell(rows[r].RowNumber(), colIndex);
                    dataRow[canonical] = cell.GetString().Trim();
                }
                if (dataRow.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    result.Add(dataRow);
            }
            return result;
        }

        private static List<Dictionary<string, string>> ParseCsv(Stream fileStream)
        {
            using var reader = new StreamReader(fileStream);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null) lines.Add(line);
            if (lines.Count == 0) return new List<Dictionary<string, string>>();

            var headerCells = SplitCsvLine(lines[0]);
            var columnMap = new Dictionary<int, string>();
            for (int i = 0; i < headerCells.Count; i++)
            {
                var canonical = ResolveCanonicalField(headerCells[i]);
                if (canonical != null) columnMap[i] = canonical;
            }

            var result = new List<Dictionary<string, string>>();
            for (int r = 1; r < lines.Count; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                var cells = SplitCsvLine(lines[r]);
                var dataRow = new Dictionary<string, string>();
                foreach (var (colIndex, canonical) in columnMap)
                {
                    if (colIndex < cells.Count) dataRow[canonical] = cells[colIndex].Trim();
                }
                if (dataRow.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    result.Add(dataRow);
            }
            return result;
        }

        // Minimal RFC4180-ish splitter: handles quoted fields with embedded commas/escaped quotes,
        // which a plain string.Split(',') breaks on for any distributor bill with a quoted "Item Name, Strength" cell.
        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else current.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
