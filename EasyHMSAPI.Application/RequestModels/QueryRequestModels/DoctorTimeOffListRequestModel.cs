using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffListRequestModel : MediatR.IRequest<DoctorTimeOffListResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; } // Added hospitalId
    }
}
