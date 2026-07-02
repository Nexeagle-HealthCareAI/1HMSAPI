using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBloodBagPoolResponseModel
    {
        public List<BloodBagDataModel> Bags { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class BloodBagDataModel
    {
        public Guid BloodBagId { get; set; }
        public string BagNumber { get; set; } = null!;
        public string Component { get; set; } = null!;
        public string BloodGroup { get; set; } = null!;
        public decimal VolumeMl { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? StorageLocation { get; set; }
        public string Status { get; set; } = null!;
        public string? ReservedForPatientId { get; set; }
        public string? CrossmatchResult { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAdmissionTransfusionHistoryResponseModel
    {
        public List<AdmissionBloodBagDataModel> ReservedBags { get; set; } = new();
        public List<TransfusionEventDataModel> Transfusions { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionBloodBagDataModel
    {
        public Guid BloodBagId { get; set; }
        public string BagNumber { get; set; } = null!;
        public string Component { get; set; } = null!;
        public string BloodGroup { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? CrossmatchResult { get; set; }
        public DateTime? ReservedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TransfusionEventDataModel
    {
        public Guid TransfusionEventId { get; set; }
        public Guid BloodBagId { get; set; }
        public string? BagNumber { get; set; }
        public string? Component { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public decimal VolumeGivenMl { get; set; }
        public string Reaction { get; set; } = null!;
        public string? ReactionNotes { get; set; }
        public string AdministeredBy { get; set; } = null!;
        public string WitnessName { get; set; } = null!;
        public Guid? ChargeEventId { get; set; }
    }
}
