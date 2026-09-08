using System;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyExternalLabsRequestModel : IRequest<GetPathologyExternalLabsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public bool IncludeInactive { get; set; }
    }
}
