using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class CreateDepartmentRequestModel : IRequest<CreateDepartmentResponseModel>
    {
        public Guid HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? CreatedByUserID { get; set; }
    }
}
