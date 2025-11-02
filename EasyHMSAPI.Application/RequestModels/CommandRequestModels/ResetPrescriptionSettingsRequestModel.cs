using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ResetPrescriptionSettingsRequestModel : IRequest<ResetPrescriptionSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
