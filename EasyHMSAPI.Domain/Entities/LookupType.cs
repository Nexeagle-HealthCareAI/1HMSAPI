namespace EasyHMSAPI.Domain.Entities
{
    public class LookupType
    {
        public int LookupTypeId { get; set; }
        public string LookupTypeCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }

        public ICollection<LookupMaster>? LookupMasters { get; set; }
        public ICollection<LookupPersonal>? LookupPersonals { get; set; }
    }
}
