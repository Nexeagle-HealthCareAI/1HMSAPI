using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AbdmCareContextDto
    {
        public Guid CareContextId { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string[] HiTypes { get; set; } = Array.Empty<string>();
        public string? AbhaAddress { get; set; }
        public string LinkStatus { get; set; } = string.Empty;
        public string? LastErrorCode { get; set; }
        public string? LastErrorMessage { get; set; }
        public DateTime? LinkedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static AbdmCareContextDto From(AbdmCareContext c) => new()
        {
            CareContextId = c.CareContextId,
            PatientId = c.PatientId,
            SourceType = c.SourceType,
            SourceId = c.SourceId,
            ReferenceNumber = c.ReferenceNumber,
            Display = c.Display,
            HiTypes = c.HiTypes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            AbhaAddress = c.AbhaAddress,
            LinkStatus = c.LinkStatus,
            LastErrorCode = c.LastErrorCode,
            LastErrorMessage = c.LastErrorMessage,
            LinkedAt = c.LinkedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    [ExcludeFromCodeCoverage]
    public class AbdmCareContextResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public AbdmCareContextDto? CareContext { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AbdmCareContextListResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AbdmCareContextDto> CareContexts { get; set; } = new();
    }
}
