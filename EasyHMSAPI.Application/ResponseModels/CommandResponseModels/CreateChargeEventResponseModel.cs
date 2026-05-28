using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateChargeEventResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public ChargeEventData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ChargeEventData
    {
        public Guid EncounterId { get; set; }
        public string? DoctorName { get; set; }
    }
}
