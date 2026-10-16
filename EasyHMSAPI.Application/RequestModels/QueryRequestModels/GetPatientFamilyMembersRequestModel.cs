using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Mobile is set by the controller from the verified patient token, never from the client.
    [ExcludeFromCodeCoverage]
    public class GetPatientFamilyMembersRequestModel : IRequest<PatientFamilyMembersResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
