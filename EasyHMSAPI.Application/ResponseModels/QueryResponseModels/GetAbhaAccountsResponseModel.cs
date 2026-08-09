using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbhaAccountSummary
    {
        public Guid AbhaAccountId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? LinkedPatientId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAbhaAccountsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AbhaAccountSummary> Accounts { get; set; } = new();
    }
}
