using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Lean, hospital-scoped doctor roster for an internal caller that already knows which
    // hospital it's asking about (Vita's voice assistant -- one HOSPITAL_ID per deployment).
    // NOT the platform-wide marketplace listing (GetPublicDoctorsRequestModel) -- HospitalId is
    // required, not optional: unlike GetDoctors there is no "every publicly-listed hospital"
    // mode here, since this bypasses IsPubliclyListed entirely.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorRosterRequestModel : IRequest<GetPublicDoctorRosterResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
