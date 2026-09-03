using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // "Compile" step: every ACTIVE batch from this vendor expiring within DaysWindow, with stock
    // remaining — the candidate list a store manager picks from before generating the debit note.
    [ExcludeFromCodeCoverage]
    public class GetRtvEligibleBatchesRequestModel : IRequest<GetRtvEligibleBatchesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid VendorId { get; set; }
        public int DaysWindow { get; set; } = 60;
    }

    [ExcludeFromCodeCoverage]
    public class GetVendorReturnsRequestModel : IRequest<GetVendorReturnsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? VendorId { get; set; }
    }
}
