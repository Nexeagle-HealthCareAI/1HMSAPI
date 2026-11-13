using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsultantFeeConfigResponseModel
    {
        public bool Success { get; set; }
        public ConsultantFeeConfigDto? FeeConfig { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConsultantFeeConfigDto
    {
        public Guid HrConsultantFeeConfigId { get; set; }
        public Guid HrEmployeeId { get; set; }
        public DateOnly EffectiveFrom { get; set; }
        public decimal MonthlyRetainer { get; set; }
        public decimal OpdSharePercent { get; set; }
        public decimal IpdVisitFee { get; set; }
        public decimal AdminSurcharge { get; set; }
        public string? SurgeryShareConfigJson { get; set; }
        public bool IsActive { get; set; }
    }
}
