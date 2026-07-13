using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // HospitalId is no longer accepted — the handler resolves it from DoctorId itself
    // (and gates on Hospital.IsPubliclyListed), never from a client-supplied value.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorAvailabilityRequestModel : IRequest<GetPublicDoctorAvailabilityResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
    }
}
