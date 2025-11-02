using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePersonalizedDataRequestModel : IRequest<DeletePersonalizedDataResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid PersonalId { get; set; }
    }
}
