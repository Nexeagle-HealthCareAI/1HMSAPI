using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A specific composition — one or more Molecules at fixed strengths in a given dosage form
    /// (e.g. "Amoxicillin 500mg + Clavulanic Acid 125mg, Tablet"). InventoryItem.SaltCompositionId
    /// links a brand/item to its composition; two items sharing a SaltCompositionId are generic
    /// substitutes for each other. Global — not per-hospital, so compositions aren't redefined
    /// per site.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SaltComposition
    {
        [Key]
        public Guid SaltCompositionId { get; set; }
        public string DisplayName { get; set; } = null!;   // "Amoxicillin 500mg + Clavulanic Acid 125mg"
        public string? DosageForm { get; set; }              // TABLET/SYRUP/INJECTION/...

        public DateTime CreatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaltCompositionComponent
    {
        [Key]
        public Guid SaltCompositionComponentId { get; set; }
        public Guid SaltCompositionId { get; set; }
        public Guid MoleculeId { get; set; }
        public decimal StrengthValue { get; set; }
        public string StrengthUnit { get; set; } = null!;   // MG/ML/MCG/IU/%
    }
}
