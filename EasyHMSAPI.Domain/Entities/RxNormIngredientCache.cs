using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class RxNormIngredientCache
    {
        [Key]
        public string IngredientName { get; set; } = string.Empty;
        public string? RxCui { get; set; }
        public string? DisplayName { get; set; }
        public string? RelatedFormsJson { get; set; }
        public bool Found { get; set; }
        public DateTime FetchedAtUtc { get; set; }
        public string? IndicationsText { get; set; }
        public string? AdverseReactionsText { get; set; }
    }
}
