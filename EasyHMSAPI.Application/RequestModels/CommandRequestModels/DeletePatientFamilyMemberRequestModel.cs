using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Removes the member and the records kept for them. Scoped to (MemberId, Mobile), so another account's member is simply "not found".
    [ExcludeFromCodeCoverage]
    public class DeletePatientFamilyMemberRequestModel : IRequest<PatientFamilyMembersResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        public Guid MemberId { get; set; }
    }
}
