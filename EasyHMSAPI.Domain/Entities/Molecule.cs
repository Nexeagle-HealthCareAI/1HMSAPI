using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A single active pharmaceutical ingredient (e.g. "Amoxicillin"), global — not per-hospital.</summary>
    [ExcludeFromCodeCoverage]
    public class Molecule
    {
        [Key]
        public Guid MoleculeId { get; set; }
        public string Name { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
