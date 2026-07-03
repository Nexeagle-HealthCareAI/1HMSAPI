using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Child table — one SterilizationCycle sterilizes multiple InstrumentSet rows.</summary>
    [ExcludeFromCodeCoverage]
    [Table("SterilizationCycleItem")]
    public class SterilizationCycleItem
    {
        [Key]
        public Guid SterilizationCycleItemId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SterilizationCycleId { get; set; }
        public Guid InstrumentSetId { get; set; }
    }
}
