using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A group of inter-connected hospitals under one owner. A hospital with ChainId = null is
    /// standalone; onboarding more hospitals under the same owner links them via this chain.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class HospitalChain
    {
        [Key]
        public Guid ChainId { get; set; }
        public string Name { get; set; } = null!;
        public Guid OwnerUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }

        public ICollection<Hospital> Hospitals { get; set; } = new List<Hospital>();
    }
}
