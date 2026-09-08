using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMoleculesResponseModel
    {
        public List<MoleculeDataModel> Molecules { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MoleculeDataModel
    {
        public Guid MoleculeId { get; set; }
        public string Name { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class GetSaltCompositionsResponseModel
    {
        public List<SaltCompositionDataModel> Compositions { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SaltCompositionDataModel
    {
        public Guid SaltCompositionId { get; set; }
        public string DisplayName { get; set; } = null!;
        public string? DosageForm { get; set; }
        public List<SaltCompositionComponentDataModel> Components { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SaltCompositionComponentDataModel
    {
        public Guid MoleculeId { get; set; }
        public string MoleculeName { get; set; } = null!;
        public decimal StrengthValue { get; set; }
        public string StrengthUnit { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class GetSubstituteItemsResponseModel
    {
        public bool HasComposition { get; set; }
        public List<SubstituteItemDataModel> Alternates { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class SubstituteItemDataModel
    {
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string? Manufacturer { get; set; }
        public decimal? DefaultRate { get; set; }
        public decimal StockAtStore { get; set; }
    }
}
