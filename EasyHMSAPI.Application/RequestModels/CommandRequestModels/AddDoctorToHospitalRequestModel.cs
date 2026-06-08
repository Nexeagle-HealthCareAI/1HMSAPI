using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Attach an existing doctor to another hospital in the caller's chain (reuses the doctor's
    /// single clinical identity). Only the chain owner may call this, and only Doctor/AdminDoctor
    /// users may be multi-hospital.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AddDoctorToHospitalRequestModel : IRequest<AddDoctorToHospitalResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid TargetHospitalId { get; set; }
        public Guid DepartmentId { get; set; }
        public decimal? ConsultFee { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
