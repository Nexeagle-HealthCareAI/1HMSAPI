using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Staff-facing "who's available today" roster — every doctor at one hospital, each resolved
    // against the same TimeOff > Override > Template precedence as the public directory badge
    // (DoctorAvailabilityResolver), for a given date (defaults to today when Date is unset).
    [ExcludeFromCodeCoverage]
    public class GetDoctorAvailabilityRosterRequestModel : IRequest<GetDoctorAvailabilityRosterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime? Date { get; set; }
    }
}
