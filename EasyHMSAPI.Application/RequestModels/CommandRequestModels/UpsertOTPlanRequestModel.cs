using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertOTPlanRequestModel : IRequest<UpsertOTPlanResponseModel>
    {
        public Guid? OtPlanId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? PlanName { get; set; }
        public string? ProcedureName { get; set; }
        public string? DefaultRoomCategory { get; set; }
        public string? SuggestedIcuLevel { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
