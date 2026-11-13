using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CheckPrescriptionSafetyResponseModel
    {
        public bool Success { get; set; } = true;
        /// <summary>False when the safety tables could not be read -- callers must say "not checked", never "safe".</summary>
        public bool Checked { get; set; }
        public string? Message { get; set; }
        public List<AllergyAlertDataModel> AllergyAlerts { get; set; } = new();
        public List<InteractionAlertDataModel> InteractionAlerts { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AllergyAlertDataModel
    {
        public string Medicine { get; set; } = null!;
        public string Allergen { get; set; } = null!;
        public string Severity { get; set; } = "MODERATE";
        public string? Reaction { get; set; }
        /// <summary>PATIENT_ALLERGY (structured record) or PROFILE (free-text allergies on the patient).</summary>
        public string Source { get; set; } = "PATIENT_ALLERGY";
    }

    [ExcludeFromCodeCoverage]
    public class InteractionAlertDataModel
    {
        public string MedicineA { get; set; } = null!;
        public string MedicineB { get; set; } = null!;
        public string Severity { get; set; } = "MODERATE";
        public string? Effect { get; set; }
        public string? Management { get; set; }
    }
}
