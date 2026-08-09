using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetOrderSetsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<OrderSetDataModel> OrderSets { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OrderSetDataModel
    {
        public Guid OrderSetId { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public List<OrderSetLineDataModel> Lines { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class OrderSetLineDataModel
    {
        public string? ItemName { get; set; }
        public string? OrderType { get; set; }
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }
        public bool IsHighAlert { get; set; }
        public decimal Qty { get; set; } = 1;
    }
}
