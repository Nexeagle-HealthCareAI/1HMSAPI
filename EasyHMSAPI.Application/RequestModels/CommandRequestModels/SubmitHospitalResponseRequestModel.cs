using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SubmitHospitalResponseRequestModel : IRequest<SubmitHospitalResponseResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string Comment { get; set; } = null!;
    }
}
