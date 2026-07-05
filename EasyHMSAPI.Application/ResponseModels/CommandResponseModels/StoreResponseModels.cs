using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertStoreResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid StoreId { get; set; }
    }
}
