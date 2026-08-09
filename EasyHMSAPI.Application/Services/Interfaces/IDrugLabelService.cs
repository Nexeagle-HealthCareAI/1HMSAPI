namespace EasyHMSAPI.Application.Services.Interfaces
{
    public class DrugLabelLookupResult
    {
        public bool Found { get; set; }
        public string? IndicationsAndUsage { get; set; }
        public string? AdverseReactions { get; set; }
    }

    public interface IDrugLabelService
    {
        Task<DrugLabelLookupResult> LookupLabelAsync(string ingredientName, CancellationToken cancellationToken);
    }
}
