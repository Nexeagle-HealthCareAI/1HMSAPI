using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDepartmentRequestModel : IRequest<UpdateDepartmentResponseModel>
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
