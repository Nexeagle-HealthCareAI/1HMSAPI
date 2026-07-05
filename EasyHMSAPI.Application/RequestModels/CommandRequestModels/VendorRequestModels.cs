using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: VendorId present => update that vendor in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class UpsertVendorRequestModel : IRequest<UpsertVendorResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? VendorId { get; set; }
        public string VendorCode { get; set; } = null!;
        public string VendorName { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? GstNumber { get; set; }
        public string? DrugLicenseNumber { get; set; }
        public int PaymentTermsDays { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
