using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: StoreId present => update that store in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class UpsertStoreRequestModel : IRequest<UpsertStoreResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? StoreId { get; set; }
        public string StoreCode { get; set; } = null!;
        public string StoreName { get; set; } = null!;
        public string StoreType { get; set; } = null!;
        public Guid? ParentStoreId { get; set; }
        public decimal? MinTempCelsius { get; set; }
        public decimal? MaxTempCelsius { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
