using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — never a
    // client-supplied query param (same convention as GetPublicAppointmentsByMobileRequestModel).
    [ExcludeFromCodeCoverage]
    public class GetHealthLockerDocumentsRequestModel : IRequest<GetHealthLockerDocumentsResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        // Whose records: a family member of this account, or empty for the account holder's own.
        public Guid? MemberId { get; set; }
        // Everyone's records on the account (the holder's and every family member's); MemberId is then ignored.
        public bool All { get; set; }
    }
}
