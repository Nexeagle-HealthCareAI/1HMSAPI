using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRapidResponseHistoryResponseModel
    {
        public List<RapidResponseDataModel> Activations { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GetOpenRapidResponsesResponseModel
    {
        public List<RapidResponseDataModel> Activations { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RapidResponseDataModel
    {
        public Guid ActivationId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? PatientName { get; set; }
        public string TriggerReason { get; set; } = null!;
        public int? TriggeredEwsScore { get; set; }
        public string CalledBy { get; set; } = null!;
        public DateTime CalledAt { get; set; }
        public string? RespondingTeam { get; set; }
        public DateTime? ArrivedAt { get; set; }
        public int? ResponseTimeSeconds { get; set; }
        public string? Outcome { get; set; }
        public string? OutcomeNotes { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
