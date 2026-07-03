using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetFluidBalanceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<FluidEntryItem> Entries { get; set; } = new();
        public List<FluidDayTotal> DailyTotals { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class FluidEntryItem
    {
        public Guid FluidEntryId { get; set; }
        public string Direction { get; set; } = null!;
        public string Subtype { get; set; } = null!;
        public decimal VolumeMl { get; set; }
        public string? Description { get; set; }
        public string? RouteOrSite { get; set; }
        public string? Colour { get; set; }
        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class FluidDayTotal
    {
        public string DayKey { get; set; } = null!;   // IST calendar date, yyyy-MM-dd
        public decimal TotalInMl { get; set; }
        public decimal TotalOutMl { get; set; }
        public decimal BalanceMl { get; set; }
    }
}
