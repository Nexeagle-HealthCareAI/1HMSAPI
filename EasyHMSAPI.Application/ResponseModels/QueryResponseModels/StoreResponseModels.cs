using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetStoresResponseModel
    {
        public List<StoreDataModel> Stores { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class StoreDataModel
    {
        public Guid StoreId { get; set; }
        public string StoreCode { get; set; } = null!;
        public string StoreName { get; set; } = null!;
        public string StoreType { get; set; } = null!;
        public string? AssignedBoard { get; set; }
        public Guid? ParentStoreId { get; set; }
        public string? ParentStoreName { get; set; }
        public decimal? MinTempCelsius { get; set; }
        public decimal? MaxTempCelsius { get; set; }
        public bool IsActive { get; set; }
    }
}
