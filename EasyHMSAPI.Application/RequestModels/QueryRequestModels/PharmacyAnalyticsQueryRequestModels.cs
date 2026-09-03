using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPharmacySalesTrendRequestModel : IRequest<GetPharmacySalesTrendResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string GroupBy { get; set; } = "DAY";   // DAY/WEEK/MONTH
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyAbcAnalysisRequestModel : IRequest<GetPharmacyAbcAnalysisResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyGstLiabilityRequestModel : IRequest<GetPharmacyGstLiabilityResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPharmacyExpiryLossPreventedRequestModel : IRequest<GetPharmacyExpiryLossPreventedResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}
