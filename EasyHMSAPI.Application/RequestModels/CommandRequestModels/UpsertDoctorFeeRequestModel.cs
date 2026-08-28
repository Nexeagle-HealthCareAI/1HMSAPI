using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertDoctorFeeRequestModel : IRequest<UpsertDoctorFeeResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public decimal OpdConsultFee { get; set; }
        public decimal IpdVisitFee { get; set; }
        public decimal EmergencyFee { get; set; }
        public int FreeFollowUpDays { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
