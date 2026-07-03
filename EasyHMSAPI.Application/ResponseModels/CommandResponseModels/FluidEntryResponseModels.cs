using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RecordFluidEntryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? FluidEntryId { get; set; }
    }
}
