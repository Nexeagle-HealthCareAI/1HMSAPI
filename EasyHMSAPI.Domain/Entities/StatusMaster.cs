using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("StatusMaster")]
    public class StatusMaster
    {
        [Key]
        [MaxLength(40)]
        public required string StatusCode { get; set; }
        [MaxLength(80)]
        public required string DisplayName { get; set; }
        public int SortOrder { get; set; }
        public bool IsTerminal { get; set; }
    }
}
