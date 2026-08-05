namespace EasyHMSAPI.Application.Services.Interfaces
{
    public class RxNormLookupResult
    {
        public bool Found { get; set; }
        public string? RxCui { get; set; }
        public string? DisplayName { get; set; }
        public List<string> AvailableForms { get; set; } = new();
    }

    public interface IRxNormService
    {
        Task<RxNormLookupResult> LookupIngredientAsync(string ingredientName, CancellationToken cancellationToken);
    }
}
