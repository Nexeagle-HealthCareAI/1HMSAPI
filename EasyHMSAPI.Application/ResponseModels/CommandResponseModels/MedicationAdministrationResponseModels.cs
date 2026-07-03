using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordMedicationAdministrationResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? MedicationAdministrationId { get; set; }
        public string? ActionStatus { get; set; }
    }
}
