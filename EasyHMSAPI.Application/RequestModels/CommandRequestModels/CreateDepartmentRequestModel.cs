using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDepartmentRequestModel : IRequest<CreateDepartmentResponseModel>
    {
        public Guid HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? CreatedByUserID { get; set; }
    }
}
