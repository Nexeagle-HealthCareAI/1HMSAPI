using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one glucose reading. ValueMgDl/IsHypo/IsHyper are never accepted from the client —
    // all 3 are server-computed (see GlucoseReadingCommandHandlers).
    [ExcludeFromCodeCoverage]
    public class RecordGlucoseReadingRequestModel : IRequest<RecordGlucoseReadingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; } = "mg/dL";
        public string? Method { get; set; }
        public string? MealTag { get; set; }

        public bool InsulinGiven { get; set; }
        public decimal? InsulinUnits { get; set; }
        public string? InsulinType { get; set; }
        public string? InsulinRoute { get; set; }

        public string? Notes { get; set; }
    }
}
