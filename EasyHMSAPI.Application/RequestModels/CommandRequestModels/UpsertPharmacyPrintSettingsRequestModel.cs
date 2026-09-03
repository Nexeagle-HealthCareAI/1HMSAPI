using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPharmacyPrintSettingsRequestModel : IRequest<UpsertPharmacyPrintSettingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? TradeName { get; set; }
        public string? Dl20BNumber { get; set; }
        public string? Dl21BNumber { get; set; }
        public string? FssaiNumber { get; set; }
        public string? PharmacistName { get; set; }
        public string? PharmacistRegNo { get; set; }
        public string? ReturnPolicyText { get; set; }
        public bool ShowVerificationQr { get; set; } = true;

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
