using System;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetLabConfigurationQuery : IRequest<LabConfiguration>
    {
        public Guid HospitalId { get; set; }
    }
}
