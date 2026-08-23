using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyOrdersQuery : IRequest<List<PathologyOrderDto>>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPathologyOrderByIdQuery : IRequest<PathologyOrderDto>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
    }
}
