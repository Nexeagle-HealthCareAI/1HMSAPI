using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Backs the Lead Generation page (easyHMSWeb) -- staff-authenticated, hospital-scoped listing
    // of HospitalLeads. HospitalAccessFilter auto-scopes this via the HospitalId property (same
    // convention every other [Authorize] hospital-scoped query in this codebase uses), no extra
    // filter wiring needed.
    [ExcludeFromCodeCoverage]
    public class GetHospitalLeadsRequestModel : IRequest<GetHospitalLeadsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Source { get; set; }
        public string? LeadType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
