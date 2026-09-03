using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateMoleculeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? MoleculeId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateSaltCompositionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? SaltCompositionId { get; set; }
    }
}
