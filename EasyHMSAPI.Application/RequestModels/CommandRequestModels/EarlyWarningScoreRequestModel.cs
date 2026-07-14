using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Same raw-inputs-in/calculator-computes-out shape as RecordSofaScoreRequestModel.
    [ExcludeFromCodeCoverage]
    public class RecordEarlyWarningScoreRequestModel : IRequest<RecordEarlyWarningScoreResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

        public int? RespiratoryRate { get; set; }
        public decimal? Spo2 { get; set; }
        public bool SupplementalOxygen { get; set; }
        public int? SystolicBp { get; set; }
        public int? Pulse { get; set; }
        public string ConsciousnessLevel { get; set; } = "ALERT";
        public decimal? TemperatureC { get; set; }

        public string? Notes { get; set; }
    }
}
