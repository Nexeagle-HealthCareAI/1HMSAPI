using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class UserStatus
    {
  [Key]
  public int UserStatusId { get; set; }
        [Required]
    [MaxLength(50)]
        public string StatusName { get; set; }

    public ICollection<UserHistory> UserHistories { get; set; }
    }
}