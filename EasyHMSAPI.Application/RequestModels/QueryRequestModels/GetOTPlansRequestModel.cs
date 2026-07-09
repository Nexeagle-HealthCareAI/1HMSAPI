using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetOTPlansRequestModel : IRequest<GetOTPlansResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? DepartmentId { get; set; }
        // When false (default), only active plans are returned — matches the Bed Master convention
        // of hiding inactive rows from normal use while keeping them recoverable.
        public bool IncludeInactive { get; set; }
    }
}
