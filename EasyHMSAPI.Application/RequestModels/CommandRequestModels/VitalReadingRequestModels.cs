using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one high-frequency IPD vital-signs reading. BMI/GcsTotal are never accepted from
    // the client — both are server-computed from the other fields (see VitalReadingCommandHandlers).
    [ExcludeFromCodeCoverage]
    public class RecordVitalReadingRequestModel : IRequest<RecordVitalReadingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }

        public decimal? Temperature { get; set; }
        public string? TemperatureUnit { get; set; }
        public int? Pulse { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? RespiratoryRate { get; set; }
        public decimal? SpO2 { get; set; }

        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }

        public int? GcsEye { get; set; }
        public int? GcsVerbal { get; set; }
        public int? GcsMotor { get; set; }

        public int? PainScore { get; set; }
        public string? Notes { get; set; }
    }
}
