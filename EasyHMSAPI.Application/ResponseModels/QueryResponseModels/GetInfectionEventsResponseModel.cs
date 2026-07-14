using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInfectionEventsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<InfectionEventItem> Events { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class InfectionEventItem
    {
        public Guid InfectionEventId { get; set; }
        public Guid? DeviceAssignmentId { get; set; }
        public string InfectionType { get; set; } = null!;
        public DateTime DiagnosedAt { get; set; }
        public string DiagnosedByDoctorName { get; set; } = null!;
        public string? CultureOrganism { get; set; }
        public string? Notes { get; set; }
    }
}
