using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Raw-inputs-in, persist-as-is -- no scoring math needed (unlike APACHE/SOFA), just structured
    // capture of the settings a doctor/nurse reads off the ventilator.
    [ExcludeFromCodeCoverage]
    public class RecordVentilatorSettingsRequestModel : IRequest<RecordVentilatorSettingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

        public string Mode { get; set; } = null!;   // AC / SIMV / PSV / CPAP -- soft-validated free text
        public decimal? FiO2Percent { get; set; }
        public decimal? PeepCmH2o { get; set; }
        public decimal? TidalVolumeMl { get; set; }
        public int? RespiratoryRateSet { get; set; }
        public decimal? PeakInspiratoryPressure { get; set; }
        public decimal? PlateauPressure { get; set; }

        public string? Notes { get; set; }
    }
}
