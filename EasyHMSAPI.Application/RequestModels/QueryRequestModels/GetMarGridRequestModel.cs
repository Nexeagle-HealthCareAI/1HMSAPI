using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // MAR grid for one admission, one calendar day at a time (IST) — prev/next day navigation is
    // a client concern of picking a different DayStartUtc, not server pagination.
    [ExcludeFromCodeCoverage]
    public class GetMarGridRequestModel : IRequest<GetMarGridResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        // Start of the IST calendar day to show, expressed in UTC (client computes this from the
        // day it wants, same toIstDate/formatIstDateTime convention as ClinicalOrderPanel.tsx).
        public DateTime DayStartUtc { get; set; }
    }
}
