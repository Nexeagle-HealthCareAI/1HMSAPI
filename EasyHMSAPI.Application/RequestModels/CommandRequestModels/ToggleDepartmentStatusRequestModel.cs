using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class ToggleDepartmentStatusRequestModel : IRequest<ToggleDepartmentStatusResponseModel>
    {
        public Guid DepartmentId { get; set; }
    }
}
