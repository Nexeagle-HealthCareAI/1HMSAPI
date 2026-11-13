using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmProfileShareItem
    {
        public Guid ProfileShareId { get; set; }
        public string? CounterId { get; set; }
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public string StatusCode { get; set; } = "NEW";
        public DateTime ReceivedAt { get; set; }
        // Set when a non-merged patient at this hospital already carries this exact ABHA number —
        // i.e. a RETURNING patient. Null = new to this facility.
        public string? ExistingPatientId { get; set; }
        public string? ExistingPatientName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAbdmProfileSharesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AbdmProfileShareItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GetAbdmFacilityResponseModel
    {
        public bool Success { get; set; }
        public string? HipId { get; set; }
        // Base of the counter QR's URL (the PHR app's share-profile page). Empty when not configured
        // for this environment.
        public string QrBaseUrl { get; set; } = string.Empty;
    }
}
