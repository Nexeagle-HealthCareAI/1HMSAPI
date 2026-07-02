using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetImplantLogResponseModel
    {
        public List<ImplantLogEntryDataModel> Entries { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ImplantLogEntryDataModel
    {
        public Guid IntraOpItemUsageId { get; set; }
        public Guid SurgeryCaseId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? ProcedureName { get; set; }
        public string ItemName { get; set; } = null!;
        public decimal Qty { get; set; }
        public string? LotNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
