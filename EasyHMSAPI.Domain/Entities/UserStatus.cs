using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class UserStatus
    {
        [Key]
        public int UserStatusId { get; set; }
        [Required]
        [MaxLength(50)]
        public string? StatusName { get; set; }
        public ICollection<UserHistory>? UserHistories { get; set; }
    }
}