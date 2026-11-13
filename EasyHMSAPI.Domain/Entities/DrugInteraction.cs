using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Global (not hospital-scoped) drug-drug interaction pair; DrugA/DrugB stored lowercase.
    /// Table + starter seed live in easyHMSDatabase (create_tables_medication_safety.sql,
    /// seed_drug_interactions.sql). The starter set is NOT a licensed clinical database.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("DrugInteraction")]
    public class DrugInteraction
    {
        [Key]
        public Guid DrugInteractionId { get; set; }
        public string DrugA { get; set; } = null!;
        public string DrugB { get; set; } = null!;
        public string Severity { get; set; } = "MODERATE";  // MINOR | MODERATE | MAJOR | CONTRAINDICATED
        public string? Effect { get; set; }
        public string? Management { get; set; }
        public string? Source { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
