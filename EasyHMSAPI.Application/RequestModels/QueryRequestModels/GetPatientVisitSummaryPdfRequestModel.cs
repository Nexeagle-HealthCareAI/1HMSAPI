using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientVisitSummaryPdfRequestModel : IRequest<GetPatientVisitSummaryPdfResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }
}
