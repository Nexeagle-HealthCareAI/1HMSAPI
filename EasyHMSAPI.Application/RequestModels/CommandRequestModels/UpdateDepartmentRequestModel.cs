using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UpdateDepartmentRequestModel : IRequest<UpdateDepartmentResponseModel>
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
