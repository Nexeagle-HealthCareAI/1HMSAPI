using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // The only path a NARCOTIC-scheduled item can be dispensed through — wraps a transaction,
    // nested-sends RecordInventoryMovementRequestModel with IsNarcoticDispenseContext=true (which
    // itself writes the NarcoticRegisterEntry row), rolling back the whole dispense if that fails.
    [ExcludeFromCodeCoverage]
    public class DispenseNarcoticRequestModel : IRequest<DispenseNarcoticResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid InventoryItemId { get; set; }
        public Guid StoreId { get; set; }
        public Guid? BatchId { get; set; }
        public decimal Qty { get; set; }
        public string PrescriberRef { get; set; } = null!;
        public string? PatientId { get; set; }
        public Guid? EncounterId { get; set; }
        public string WitnessBy { get; set; } = null!;
        public Guid? WitnessByUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordColdChainReadingRequestModel : IRequest<RecordColdChainReadingResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid StoreId { get; set; }
        public decimal TempCelsius { get; set; }
        public DateTime? RecordedAt { get; set; }
    }
}
