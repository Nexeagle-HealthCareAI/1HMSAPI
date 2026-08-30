using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PathologyOrderDto
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = null!;
        public string PatientId { get; set; } = null!;
        public string PatientName { get; set; } = null!;
        public int? PatientAgeYears { get; set; }
        public string? PatientGender { get; set; }

        public List<PathologyOrderLineDto> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PathologyOrderLineDto
    {
        public Guid OrderLineId { get; set; }
        public Guid TestId { get; set; }
        public string TestName { get; set; } = null!;
        public string TestCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? ParameterSchemaJson { get; set; }
        
        public PathologyResultDto? Result { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyResultDto
    {
        public Guid ResultId { get; set; }
        public string ResultValuesJson { get; set; } = "{}";
        public string? Interpretation { get; set; }
    }
}
