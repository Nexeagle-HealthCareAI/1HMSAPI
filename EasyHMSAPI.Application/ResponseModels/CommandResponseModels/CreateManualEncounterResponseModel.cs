using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateManualEncounterResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public ManualEncounterData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ManualEncounterData
    {
        public Guid EncounterId { get; set; }
        public string? DoctorName { get; set; }
    }
}
