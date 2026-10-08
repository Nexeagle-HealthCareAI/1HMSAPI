using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ToggleDepartmentStatusRequestModel : IRequest<ToggleDepartmentStatusResponseModel>
    {
        /// <summary>Stamped by the controller from the verified JWT; any client-supplied value is overwritten.</summary>
        public Guid? CallerUserId { get; set; }
        public Guid DepartmentId { get; set; }
    }
}
