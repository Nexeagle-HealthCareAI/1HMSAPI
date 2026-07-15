using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Rich, hospital-scoped doctor list purpose-built for the admin Public Directory tile editor —
    // deliberately separate from GetHospitalDoctorsRequestModel (the lean admit-form picker), so
    // adding photo/bio/specializations/languages resolution here never slows down that hot,
    // frequently-hit simple dropdown.
    [ExcludeFromCodeCoverage]
    public class GetPublicDirectoryDoctorsRequestModel : IRequest<GetPublicDirectoryDoctorsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
