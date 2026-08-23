using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyTestsQuery : IRequest<List<PathologyTestMaster>>
    {
        public Guid HospitalId { get; set; }
        public string? SearchTerm { get; set; }
        public string? Category { get; set; }
    }
}
