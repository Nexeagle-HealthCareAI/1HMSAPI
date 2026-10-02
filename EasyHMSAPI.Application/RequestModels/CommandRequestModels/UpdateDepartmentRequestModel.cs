using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDepartmentRequestModel : IRequest<UpdateDepartmentResponseModel>
    {
        /// <summary>Stamped by the controller from the verified JWT; any client-supplied value is overwritten.</summary>
        public Guid? CallerUserId { get; set; }
        public Guid DepartmentId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
