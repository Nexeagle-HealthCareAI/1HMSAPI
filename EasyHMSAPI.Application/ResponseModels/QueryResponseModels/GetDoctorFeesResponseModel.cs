using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorFeesResponseModel
    {
        public List<DoctorFeeRow> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DoctorFeeRow
    {
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? DepartmentName { get; set; }
        public decimal OpdConsultFee { get; set; }
        public decimal IpdVisitFee { get; set; }
        public decimal EmergencyFee { get; set; }
        // OPD_CONSULT only. Days a follow-up stays free after a paid visit. 0 = no free window,
        // every visit is chargeable.
        public int FreeFollowUpDays { get; set; }
    }
}
