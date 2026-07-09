using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class OTPlanPackageType
    {
        public Guid OtPlanId { get; set; }
        public Guid PackageTypeId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
