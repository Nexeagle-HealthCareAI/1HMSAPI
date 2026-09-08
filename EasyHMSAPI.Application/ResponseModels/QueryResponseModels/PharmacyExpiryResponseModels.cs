using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // Expiry buckets — Green/Yellow/Orange/Red thresholds, in one place so the report handler and
    // any other caller (POS cart preview, etc.) compute the same buckets the same way.
    [ExcludeFromCodeCoverage]
    public static class ExpiryBucketCalculator
    {
        public const string Green = "GREEN";
        public const string Yellow = "YELLOW";
        public const string Orange = "ORANGE";
        public const string Red = "RED";

        public static string Compute(DateTime? expiryDate, DateTime today)
        {
            if (!expiryDate.HasValue) return Green;
            var daysToExpiry = (expiryDate.Value.Date - today.Date).TotalDays;
            if (daysToExpiry < 30) return Red;
            if (daysToExpiry < 90) return Orange;
            if (daysToExpiry < 180) return Yellow;
            return Green;
        }
    }

    [ExcludeFromCodeCoverage]
    public class GetNearExpiryReportResponseModel
    {
        public List<NearExpiryBatchDataModel> Batches { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class NearExpiryBatchDataModel
    {
        public Guid BatchId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string? ItemName { get; set; }
        public string? GenericName { get; set; }
        public Guid StoreId { get; set; }
        public string? StoreName { get; set; }
        public Guid? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string BatchNumber { get; set; } = null!;
        public DateTime? ExpiryDate { get; set; }
        public int? DaysToExpiry { get; set; }
        public string Bucket { get; set; } = null!;
        public decimal RemainingQty { get; set; }
        public decimal? Mrp { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetDrugScheduleRegisterResponseModel
    {
        public List<DrugScheduleRegisterEntryDataModel> Entries { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DrugScheduleRegisterEntryDataModel
    {
        public Guid RegisterEntryId { get; set; }
        public string? ItemName { get; set; }
        public string? BatchNumber { get; set; }
        public string? StoreName { get; set; }
        public string ScheduleClass { get; set; } = null!;
        public decimal Qty { get; set; }
        public string? PatientId { get; set; }
        public string? PrescriberRef { get; set; }
        public string? DispensedBy { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
