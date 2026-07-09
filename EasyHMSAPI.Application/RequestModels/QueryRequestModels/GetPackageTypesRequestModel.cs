using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPackageTypesRequestModel : IRequest<GetPackageTypesResponseModel>
    {
        public Guid HospitalId { get; set; }
        // When false (default), only active package types are returned — matches the OT Plan
        // and Bed Master convention of hiding inactive rows from normal use.
        public bool IncludeInactive { get; set; }
    }
}
