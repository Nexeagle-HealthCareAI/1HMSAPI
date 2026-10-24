using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Staff opened an appointment request: record that the hospital has seen it (first time only).</summary>
    [ExcludeFromCodeCoverage]
    public class MarkAppointmentSeenRequestModel : IRequest<MarkAppointmentSeenResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public Guid HospitalId { get; set; }

        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
