using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PrescriptionInvestigation
    {
        [Key]
        public Guid PresInvestigationId { get; set; }
        public Guid PrescriptionId { get; set; }
        public  int LookupTypeId { get; set; }
        public string? OrdersType { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdateBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}