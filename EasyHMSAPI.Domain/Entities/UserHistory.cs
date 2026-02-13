using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class UserHistory
    {
        [Key]
        [Column(Order = 0)]
        public Guid UserId { get; set; }

        [Key]
        [Column(Order = 1)]
        public DateTime UpdatedDate { get; set; }

        [Required]
        public int UserStatusId { get; set; }

        [Required]
        public Guid UpdatedBy { get; set; }

        [ForeignKey("UserStatusId")]
        public UserStatus? UserStatus { get; set; }
    }
}