using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateMoleculeRequestModel : IRequest<CreateMoleculeResponseModel>
    {
        public string Name { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class SaltCompositionComponentInput
    {
        public Guid MoleculeId { get; set; }
        public decimal StrengthValue { get; set; }
        public string StrengthUnit { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class CreateSaltCompositionRequestModel : IRequest<CreateSaltCompositionResponseModel>
    {
        public string DisplayName { get; set; } = null!;
        public string? DosageForm { get; set; }
        public List<SaltCompositionComponentInput> Components { get; set; } = new();

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
