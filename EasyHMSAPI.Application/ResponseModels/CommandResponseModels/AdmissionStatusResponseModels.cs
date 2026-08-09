using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DischargeAdmissionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public DateTime? DischargedAt { get; set; }
        public bool BedReleased { get; set; }
        // Informational only -- discharge already happened by the time these are set. Never used
        // to block discharge; surfaced so the front desk can follow up on collection afterward.
        public bool HasOutstandingBalance { get; set; }
        public decimal OutstandingAmount { get; set; }
        public bool HasUnfinalizedInvoice { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionStatusResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public string? StatusCode { get; set; }
        public bool BedReleased { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConfirmPatientArrivalResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public Guid? BedId { get; set; }
        public Guid? BedAssignmentId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionDetailsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AdmissionId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpsertAdmissionCoverageResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? CoverageId { get; set; }
    }
}
