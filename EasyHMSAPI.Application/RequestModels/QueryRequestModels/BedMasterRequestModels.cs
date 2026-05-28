using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBedMastersRequestModel : IRequest<GetBedMastersResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    [ExcludeFromCodeCoverage]
    public class GetBedMasterByIdRequestModel : IRequest<BedMasterDetailResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid BedId { get; set; }
    }
}
