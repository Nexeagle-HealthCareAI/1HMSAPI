using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§7.6 Find ABHA — step 1 (search by mobile or Aadhaar).</summary>
    [ExcludeFromCodeCoverage]
    public class FindAbhaSearchRequestModel : IRequest<AbdmFindAbhaSearchResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string Value { get; set; } = string.Empty;
        // "mobile" | "aadhaar".
        public string SearchBy { get; set; } = "mobile";
    }
}
