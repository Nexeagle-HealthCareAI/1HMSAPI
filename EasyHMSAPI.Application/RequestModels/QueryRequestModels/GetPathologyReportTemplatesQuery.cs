using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyReportTemplatesQuery : IRequest<List<PathologyReportTemplate>>
    {
        public Guid HospitalId { get; set; }
    }
}
