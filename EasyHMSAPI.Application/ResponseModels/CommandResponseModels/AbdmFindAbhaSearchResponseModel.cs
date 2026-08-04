using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmFindAbhaCandidateModel
    {
        public int Index { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Gender { get; set; }
    }

    /// <summary>§7.6 Find ABHA — search step. A mobile/Aadhaar number can be linked to more than one
    /// ABHA number, so the caller picks one (by Index) before an OTP is sent for that candidate.</summary>
    [ExcludeFromCodeCoverage]
    public class AbdmFindAbhaSearchResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TxnId { get; set; }
        public List<AbdmFindAbhaCandidateModel> Candidates { get; set; } = new();
    }
}
