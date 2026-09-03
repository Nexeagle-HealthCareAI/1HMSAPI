using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class PreviewBulkImportHandler : IRequestHandler<PreviewBulkImportRequestModel, PreviewBulkImportResponseModel>
    {
        private readonly AppDbContext _context;

        private static readonly string[] KnownFields =
        {
            "STORECODE", "ITEMCODE", "BATCHNUMBER", "MANUFACTUREDATE", "EXPIRYDATE",
            "RECEIVEDQTY", "UNITCOST", "MRP", "BARCODEVALUE",
        };

        public PreviewBulkImportHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PreviewBulkImportResponseModel> Handle(PreviewBulkImportRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new PreviewBulkImportResponseModel { Success = false, Message = "HospitalId is required." };
            if (request.File == null || request.File.Length == 0)
                return new PreviewBulkImportResponseModel { Success = false, Message = "No file uploaded." };

            List<Dictionary<string, string>> rawRows;
            try
            {
                await using var stream = request.File.OpenReadStream();
                rawRows = BulkImportFileParser.Parse(stream, request.File.FileName);
            }
            catch (Exception ex)
            {
                return new PreviewBulkImportResponseModel { Success = false, Message = $"Could not read file: {ex.Message}" };
            }

            if (rawRows.Count == 0)
                return new PreviewBulkImportResponseModel { Success = false, Message = "No data rows found in the file." };

            var recognizedFields = rawRows.SelectMany(r => r.Keys).Distinct().ToHashSet();
            var unrecognized = KnownFields.Where(f => !recognizedFields.Contains(f)).ToList();

            var stores = await _context.Store
                .Where(s => s.HospitalId == request.HospitalId)
                .Select(s => s.StoreCode)
                .ToListAsync(cancellationToken);
            var storeCodesUpper = stores.Select(s => s.ToUpperInvariant()).ToHashSet();

            var itemCodes = await _context.InventoryItem
                .Where(i => i.HospitalId == request.HospitalId)
                .Select(i => i.ItemCode)
                .ToListAsync(cancellationToken);
            var itemCodesUpper = itemCodes.Select(i => i.ToUpperInvariant()).ToHashSet();

            var result = new PreviewBulkImportResponseModel { Success = true, UnrecognizedColumns = unrecognized };

            for (int i = 0; i < rawRows.Count; i++)
            {
                var raw = rawRows[i];
                var row = new BulkImportPreviewRow { RowIndex = i };
                var errors = new List<string>();

                row.StoreCode = raw.GetValueOrDefault("STORECODE")?.Trim();
                row.ItemCode = raw.GetValueOrDefault("ITEMCODE")?.Trim();
                row.BatchNumber = raw.GetValueOrDefault("BATCHNUMBER")?.Trim();
                row.BarcodeValue = raw.GetValueOrDefault("BARCODEVALUE")?.Trim();

                if (string.IsNullOrWhiteSpace(row.StoreCode)) errors.Add("Store code is missing.");
                else if (!storeCodesUpper.Contains(row.StoreCode.ToUpperInvariant())) errors.Add($"Store code '{row.StoreCode}' not found.");

                if (string.IsNullOrWhiteSpace(row.ItemCode)) errors.Add("Item code is missing.");
                else if (!itemCodesUpper.Contains(row.ItemCode.ToUpperInvariant())) errors.Add($"Item code '{row.ItemCode}' not found.");

                if (string.IsNullOrWhiteSpace(row.BatchNumber)) errors.Add("Batch number is missing.");

                row.ManufactureDate = TryParseDate(raw.GetValueOrDefault("MANUFACTUREDATE"));
                row.ExpiryDate = TryParseDate(raw.GetValueOrDefault("EXPIRYDATE"));
                if (!string.IsNullOrWhiteSpace(raw.GetValueOrDefault("EXPIRYDATE")) && row.ExpiryDate == null)
                    errors.Add($"Expiry date '{raw["EXPIRYDATE"]}' could not be parsed.");

                var qtyRaw = raw.GetValueOrDefault("RECEIVEDQTY");
                if (string.IsNullOrWhiteSpace(qtyRaw) || !decimal.TryParse(qtyRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                    errors.Add("Quantity is missing or not a positive number.");
                else
                    row.ReceivedQty = qty;

                row.UnitCost = TryParseDecimal(raw.GetValueOrDefault("UNITCOST"));
                row.Mrp = TryParseDecimal(raw.GetValueOrDefault("MRP"));

                row.IsValid = errors.Count == 0;
                row.ErrorMessage = errors.Count > 0 ? string.Join(" ", errors) : null;
                result.Rows.Add(row);
            }

            result.Message = $"Parsed {result.Rows.Count} rows — {result.Rows.Count(r => r.IsValid)} valid, {result.Rows.Count(r => !r.IsValid)} need correction.";
            return result;
        }

        private static DateTime? TryParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            // Distributor bills commonly use MM/YY or MM-YYYY for expiry alongside full dates.
            string[] formats = { "dd/MM/yyyy", "dd-MM-yyyy", "MM/yyyy", "MM-yyyy", "yyyy-MM-dd", "d/M/yyyy", "M/yyyy" };
            if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
            if (DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
            return null;
        }

        private static decimal? TryParseDecimal(string? raw) =>
            !string.IsNullOrWhiteSpace(raw) && decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
