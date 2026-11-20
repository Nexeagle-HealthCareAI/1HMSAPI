using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmEnrollResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TxnId { get; set; }
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public bool MobileVerified { get; set; }
        public bool IsNew { get; set; }
        // Populated only when the caller asked this step to persist the account (CreateAbhaAddress).
        public Guid? AbhaAccountId { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public string? ProfilePhoto { get; set; }
        /// <summary>One-time proof that this ABHA was verified here, for registering the patient (see accounts/link).</summary>
        public string? LinkToken { get; set; }
    }
}
