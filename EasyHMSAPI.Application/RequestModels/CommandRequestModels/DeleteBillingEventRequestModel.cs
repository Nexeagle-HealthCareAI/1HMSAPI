using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteBillingEventRequestModel : IRequest<DeleteBillingEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EventId { get; set; }
        public string? Type { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
