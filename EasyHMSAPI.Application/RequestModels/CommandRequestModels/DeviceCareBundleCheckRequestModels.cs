using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CareBundleItemResult
    {
        public string Key { get; set; } = null!;
        public bool Compliant { get; set; }
    }

    // Raw item ticks in; the handler validates keys against IpdConstants.CareBundleItems
    // for the device's type and computes CompliantCount/TotalItems/AllCompliant itself --
    // same "server computes, never trusts a client rollup" principle as EarlyWarningScoreCalculator.
    [ExcludeFromCodeCoverage]
    public class SubmitDeviceCareBundleCheckRequestModel : IRequest<SubmitDeviceCareBundleCheckResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid DeviceAssignmentId { get; set; }
        public List<CareBundleItemResult> Items { get; set; } = new();
        public string? Notes { get; set; }
    }
}
