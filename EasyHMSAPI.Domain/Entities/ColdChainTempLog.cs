using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class ColdChainTempLog
    {
        [Key]
        public Guid LogId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid StoreId { get; set; }

        public DateTime RecordedAt { get; set; }
        public decimal TempCelsius { get; set; }
        public string? RecordedBy { get; set; }
        public bool BreachFlag { get; set; }
    }
}
