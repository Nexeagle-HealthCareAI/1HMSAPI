using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Raw inputs — pre-filled client-side from GetApacheIIAutoFillRequestModel, still validated
    // server-side. ApacheIIScoreCalculator computes TotalScore; the handler never trusts a
    // client-supplied total.
    [ExcludeFromCodeCoverage]
    public class RecordApacheIIScoreRequestModel : IRequest<RecordApacheIIScoreResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

        public decimal? Temperature { get; set; }
        public int? MapValue { get; set; }
        public int? HeartRate { get; set; }
        public int? RespiratoryRate { get; set; }
        public decimal? FiO2 { get; set; }
        public decimal? PaO2 { get; set; }
        public decimal? ArterialPh { get; set; }
        public int? SerumSodium { get; set; }
        public decimal? SerumPotassium { get; set; }
        public decimal? SerumCreatinine { get; set; }
        public bool IsAcuteRenalFailure { get; set; }
        public decimal? Hematocrit { get; set; }
        public decimal? Wbc { get; set; }
        public int? GcsTotal { get; set; }

        public int? AgeYears { get; set; }
        public string ChronicHealthCategory { get; set; } = "NONE";

        public string? Notes { get; set; }
    }
}
