using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertOrderSetResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? OrderSetId { get; set; }
    }
}
