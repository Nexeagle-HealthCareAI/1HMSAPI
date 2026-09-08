using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBloodBagPoolRequestModel : IRequest<GetBloodBagPoolResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Component { get; set; }
        public string? BloodGroup { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAdmissionTransfusionHistoryRequestModel : IRequest<GetAdmissionTransfusionHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }

    // Hospital-wide stock view for the Blood Bank management screen -- unlike GetBloodBagPool
    // (Available only, for the reserve-a-unit picker), this returns every bag regardless of status
    // so staff can see Reserved/Transfused/Discarded units too, optionally filtered to one status.
    [ExcludeFromCodeCoverage]
    public class GetBloodBankInventoryRequestModel : IRequest<GetBloodBankInventoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    // Hospital-wide transfusion ledger -- every transfusion across every admission, newest first.
    [ExcludeFromCodeCoverage]
    public class GetBloodBankLedgerRequestModel : IRequest<GetBloodBankLedgerResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
