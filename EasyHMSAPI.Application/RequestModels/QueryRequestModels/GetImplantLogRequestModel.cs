using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Implant traceability/recall search over IntraOpItemUsage (Category=IMPLANT) — no separate
    // ImplantLog table. Supply LotNumber/SerialNumber for a recall lookup, or AdmissionId for a
    // per-patient traceability view.
    [ExcludeFromCodeCoverage]
    public class GetImplantLogRequestModel : IRequest<GetImplantLogResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? LotNumber { get; set; }
        public string? SerialNumber { get; set; }
        public Guid? AdmissionId { get; set; }
    }
}
