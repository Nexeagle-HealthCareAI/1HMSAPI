using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Patient-initiated account deletion (Doctor Dekho app/website; required by Apple guideline 5.1.1(v)).
    // Mobile is always the OTP-verified one from the patient token, set by PublicController — never the body.
    [ExcludeFromCodeCoverage]
    public class DeletePatientAccountRequestModel : IRequest<DeletePatientAccountResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
