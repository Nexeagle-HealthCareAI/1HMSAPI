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

        // OPD consult auto-charge outcome (surfaced so the client knows whether/what to collect).
        public bool ConsultChargePosted { get; set; }        // a consult charge was posted on this call
        public decimal ConsultFee { get; set; }              // the fee amount (0 when none)
        public bool ConsultAlreadyCharged { get; set; }      // an existing consult charge was found (idempotent reuse)
        public Guid? ConsultChargeEventId { get; set; }
    }
}
