using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ToggleDepartmentStatusRequestModel : IRequest<ToggleDepartmentStatusResponseModel>
    {
        public Guid DepartmentId { get; set; }
    }
}
