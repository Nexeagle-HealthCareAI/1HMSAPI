using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Expense
    {
        [Key]
        public Guid ExpenseId { get; set; }
        public Guid HospitalId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string? Vendor { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMode { get; set; }
        public string StatusCode { get; set; } = "PAID";
        public string? ReferenceNo { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
