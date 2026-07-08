using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetOperationTheatresResponseModel
    {
        public List<OperationTheatreDataModel> Theatres { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OperationTheatreDataModel
    {
        public Guid TheatreId { get; set; }
        public string TheatreCode { get; set; } = null!;
        public string TheatreName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public bool IsActive { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public decimal Price { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetOTScheduleResponseModel
    {
        public List<OTBookingDataModel> Bookings { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OTBookingDataModel
    {
        public Guid OTBookingId { get; set; }
        public Guid SurgeryCaseId { get; set; }
        public Guid TheatreId { get; set; }
        public string? TheatreCode { get; set; }
        public string? TheatreName { get; set; }
        public string? ProcedureName { get; set; }
        public string? PatientName { get; set; }
        public DateTime ScheduledStart { get; set; }
        public DateTime ScheduledEnd { get; set; }
        public string StatusCode { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class GetOtBoardResponseModel
    {
        public List<OtBoardCaseDataModel> Cases { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class OtBoardCaseDataModel
    {
        public Guid SurgeryCaseId { get; set; }
        public string StatusCode { get; set; } = null!;
        public string? PatientName { get; set; }
        public string ProcedureName { get; set; } = null!;
        public string? SurgeonName { get; set; }
        public string SurgeryType { get; set; } = null!;
        public string Urgency { get; set; } = null!;
        public Guid? TheatreId { get; set; }
        public string? TheatreName { get; set; }
        public DateTime? ScheduledStart { get; set; }
        public DateTime? ScheduledEnd { get; set; }
        public Guid? EncounterId { get; set; }
        public Guid AdmissionId { get; set; }
        // Drives the card's progress readout — which safety-gate items are done so far.
        public bool PreOpAssessmentComplete { get; set; }
        public bool SignInComplete { get; set; }
        public bool TimeOutComplete { get; set; }
        public bool SignOutComplete { get; set; }
    }
}
