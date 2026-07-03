using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Settles a batch of a doctor's ACCRUED ledger entries to PAID. When LedgerIds is empty/null,
    // settles every currently-ACCRUED entry for the doctor — the common "pay out everything owed"
    // case. TdsAmount is the total withheld across the whole batch (194H-style), not per-line.
    [ExcludeFromCodeCoverage]
    public class SettleConsultantIncentivesRequestModel : IRequest<SettleConsultantIncentivesResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid DoctorId { get; set; }
        public List<Guid>? LedgerIds { get; set; }
        public string? PayoutRef { get; set; }
        public decimal? TdsAmount { get; set; }
    }
}
