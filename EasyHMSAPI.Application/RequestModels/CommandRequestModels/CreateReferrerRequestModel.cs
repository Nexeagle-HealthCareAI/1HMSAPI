using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateReferrerRequestModel : IRequest<CreateReferrerResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public string ReferrerName { get; set; } = string.Empty;
        public string ReferrerType { get; set; } = "REFERRER";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Pan { get; set; }
        public decimal DefaultRatePercent { get; set; }
        [JsonIgnore]
        public Guid? UserId { get; set; }
    }
}
