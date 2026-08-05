using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetMedicineInfoResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? MedicineName { get; set; }
        public List<IngredientInfoDataModel> Ingredients { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IngredientInfoDataModel
    {
        public string IngredientName { get; set; } = string.Empty;
        public bool Found { get; set; }
        public string? RxCui { get; set; }
        public string? DisplayName { get; set; }
        public List<string> AvailableForms { get; set; } = new();
    }
}
