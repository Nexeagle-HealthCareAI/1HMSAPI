using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class LookupMaster
    {
        public Guid LookupId { get; set; }
        public int LookupTypeId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public string? Synonyms { get; set; }
        public string? MetaJson { get; set; }
        public bool IsActive { get; set; }
        public bool IsPinned { get; set; }
        public long UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
        public LookupType? LookupType { get; set; }
        public ICollection<LookupPersonal>? LookupPersonals { get; set; }
    }
}
