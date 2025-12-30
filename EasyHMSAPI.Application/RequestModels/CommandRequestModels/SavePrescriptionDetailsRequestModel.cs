using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class SavePrescriptionDetailsRequestModel : IRequest<SavePrescriptionDetailsResponseModel>
    {
        public Guid? PrescriptionId { get; set; }
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
        public PatientVitalsModel? VitalsJson { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? History { get; set; }
        public string? Comorbidity { get; set; }
        public string? Examination { get; set; }
        public string? Diagnosis { get; set; }
        public OrdersModel? Orders { get; set; }
        public List<MedicationModel>? Medications { get; set; }
        public List<NonPharmacologicalAdviceModel>? NonPharmacologicalAdvice { get; set; }
        public string? PrivateNotes { get; set; }
        public CertificateDataModel? Certificates { get; set; }
        public FollowUpModel? FollowUp { get; set; }
        public List<ImmunizationModel>? Immunizations { get; set; }
        [JsonIgnore]
        public string? ActionType { get; set; }
        [JsonIgnore]
        public DateTime CurrentDateTime { get; set; }
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class OrdersModel
    {
        public List<string>? Investigations { get; set; }
        public List<string>? Procedures { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MedicationModel
    {
        public string? DrugName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public string? Duration { get; set; }
        public string? Instructions { get; set; }
        public string? SaltName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NonPharmacologicalAdviceModel
    {
        public string? Advice { get; set; }
        public string? Duration { get; set; }
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CertificateDataModel
    {
        public string? Type { get; set; }
        public string? Content { get; set; }
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? IssuedDate { get; set; }
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? FromDate { get; set; }
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? ToDate { get; set; }
        public string? FitnessStatus { get; set; }
        public string? Remarks { get; set; }
        public string? Category { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class FollowUpModel
    {
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? FollowUpOn { get; set; }
        public string? Reason { get; set; }
        public string? PatientInstructions { get; set; }
        public bool? ReferralEnabled { get; set; }
        public ReferralModel? Referral { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReferralModel
    {
        public ReferredToModel? ReferredTo { get; set; }
        public string? ClinicalSummary { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReferredToModel
    {
        public string? Specialty { get; set; }
        public string? DoctorName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ImmunizationModel
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? Date { get; set; }
        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? NextDueDate { get; set; }
        public int? DoseNumber { get; set; }
        public string? Remarks { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return null;
                    if (DateTime.TryParse(stringValue, out var dateTime))
                        return dateTime;
                    throw new JsonException($"Unable to convert \"{stringValue}\" to DateTime.");
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} when parsing DateTime.");
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("o"));
            else
                writer.WriteNullValue();
        }
    }
}
