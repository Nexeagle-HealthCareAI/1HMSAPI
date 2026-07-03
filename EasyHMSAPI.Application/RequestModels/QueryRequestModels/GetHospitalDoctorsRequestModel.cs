using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Flat list of every doctor at one hospital — no department filter, unlike
    // GetDepartmentDoctorsRequestModel. Used for simple hospital-wide pickers (e.g. the
    // admitting-consultant selector on the admit form).
    [ExcludeFromCodeCoverage]
    public class GetHospitalDoctorsRequestModel : IRequest<GetHospitalDoctorsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
