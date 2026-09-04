using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyExternalLabsResponseModel
    {
        public List<PathologyExternalLabDataModel> Labs { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PathologyExternalLabDataModel
    {
        public Guid ExternalLabId { get; set; }
        public string LabName { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? AccreditationNo { get; set; }
        public bool IsActive { get; set; }
    }
}
