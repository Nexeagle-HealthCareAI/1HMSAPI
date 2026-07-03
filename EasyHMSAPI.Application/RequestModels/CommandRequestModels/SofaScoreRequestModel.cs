using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Same raw-inputs-in/calculator-computes-out shape as RecordApacheIIScoreRequestModel.
    [ExcludeFromCodeCoverage]
    public class RecordSofaScoreRequestModel : IRequest<RecordSofaScoreResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

        public decimal? PaO2FiO2Ratio { get; set; }
        public bool OnRespiratorySupport { get; set; }
        public decimal? PlateletsCount { get; set; }
        public decimal? BilirubinMgDl { get; set; }
        public int? MapValue { get; set; }
        public string VasopressorTier { get; set; } = "NONE";
        public int? GcsTotal { get; set; }
        public decimal? CreatinineMgDl { get; set; }
        public decimal? UrineOutputMlPerDay { get; set; }

        public string? Notes { get; set; }
    }
}
