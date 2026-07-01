using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one nursing-assessment snapshot (Morse Fall Scale + Braden Pressure-Ulcer Scale +
    // MUST nutrition screen). Only the raw component values are accepted — every *Total/*Risk
    // field is server-computed (see NursingAssessmentCommandHandlers). Insert-only: re-assess by
    // submitting a new snapshot, never update an existing one.
    [ExcludeFromCodeCoverage]
    public class RecordNursingAssessmentRequestModel : IRequest<RecordNursingAssessmentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }

        public int MorseHistoryOfFalling { get; set; }
        public int MorseSecondaryDiagnosis { get; set; }
        public int MorseAmbulatoryAid { get; set; }
        public int MorseIvHeparinLock { get; set; }
        public int MorseGait { get; set; }
        public int MorseMentalStatus { get; set; }

        public int BradenSensoryPerception { get; set; }
        public int BradenMoisture { get; set; }
        public int BradenActivity { get; set; }
        public int BradenMobility { get; set; }
        public int BradenNutrition { get; set; }
        public int BradenFrictionShear { get; set; }

        public int MustBmiScore { get; set; }
        public int MustWeightLossScore { get; set; }
        public int MustAcuteDiseaseScore { get; set; }

        public string? Notes { get; set; }
    }
}
