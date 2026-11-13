using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Mobile is set by the controller from the already-validated patient JWT claim — never from the body,
    // or any caller could erase someone else's account.
    [ExcludeFromCodeCoverage]
    public class DeletePatientAccountRequestModel : IRequest<DeletePatientAccountResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
