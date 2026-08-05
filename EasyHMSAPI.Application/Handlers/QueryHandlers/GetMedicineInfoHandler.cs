using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetMedicineInfoHandler : IRequestHandler<GetMedicineInfoRequestModel, GetMedicineInfoResponseModel>
    {
        // Ingredient/salt identity doesn't change - a month-long cache keeps us off RxNorm for
        // anything already resolved, while still picking up occasional NLM catalog updates.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

        private readonly AppDbContext _dbContext;
        private readonly IRxNormService _rxNormService;

        public GetMedicineInfoHandler(AppDbContext dbContext, IRxNormService rxNormService)
        {
            _dbContext = dbContext;
            _rxNormService = rxNormService;
        }

        public async Task<GetMedicineInfoResponseModel> Handle(GetMedicineInfoRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GetMedicineInfoResponseModel { Success = false };

            var medicine = await _dbContext.MedicineMaster
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MedicineId == request.MedicineId, cancellationToken);

            if (medicine == null)
            {
                response.Message = "Medicine not found.";
                return response;
            }

            response.MedicineName = medicine.MedicineName;

            if (string.IsNullOrWhiteSpace(medicine.GenericName))
            {
                response.Success = true;
                response.Message = "No composition data available for this medicine.";
                return response;
            }

            foreach (var ingredient in SplitIngredients(medicine.GenericName))
            {
                response.Ingredients.Add(await ResolveIngredientAsync(ingredient, cancellationToken));
            }

            response.Success = true;
            response.Message = "Medicine info retrieved successfully.";
            return response;
        }

        // "Aspirin (75mg) + Rosuvastatin (20mg) + Clopidogrel (75mg)" -> ["Aspirin", "Rosuvastatin", "Clopidogrel"]
        private static List<string> SplitIngredients(string genericName)
        {
            return genericName
                .Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var parenIndex = part.IndexOf('(');
                    var name = parenIndex > 0 ? part[..parenIndex] : part;
                    return name.Trim();
                })
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<IngredientInfoDataModel> ResolveIngredientAsync(string ingredientName, CancellationToken cancellationToken)
        {
            var cacheKey = ingredientName.Trim().ToLowerInvariant();
            var cached = await _dbContext.RxNormIngredientCache
                .FirstOrDefaultAsync(c => c.IngredientName == cacheKey, cancellationToken);

            if (cached != null && DateTime.UtcNow - cached.FetchedAtUtc < CacheTtl)
                return ToDataModel(ingredientName, cached);

            RxNormLookupResult lookup;
            try
            {
                lookup = await _rxNormService.LookupIngredientAsync(ingredientName, cancellationToken);
            }
            catch (Exception)
            {
                // RxNorm being slow/unreachable shouldn't fail the whole request - just report
                // this one ingredient as not found and let a later call retry (nothing is cached).
                return new IngredientInfoDataModel { IngredientName = ingredientName, Found = false };
            }

            var entity = cached ?? new RxNormIngredientCache { IngredientName = cacheKey };
            entity.Found = lookup.Found;
            entity.RxCui = lookup.RxCui;
            entity.DisplayName = lookup.DisplayName;
            entity.RelatedFormsJson = JsonSerializer.Serialize(lookup.AvailableForms);
            entity.FetchedAtUtc = DateTime.UtcNow;

            if (cached == null)
                _dbContext.RxNormIngredientCache.Add(entity);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new IngredientInfoDataModel
            {
                IngredientName = ingredientName,
                Found = lookup.Found,
                RxCui = lookup.RxCui,
                DisplayName = lookup.DisplayName,
                AvailableForms = lookup.AvailableForms,
            };
        }

        private static IngredientInfoDataModel ToDataModel(string ingredientName, RxNormIngredientCache cached)
        {
            return new IngredientInfoDataModel
            {
                IngredientName = ingredientName,
                Found = cached.Found,
                RxCui = cached.RxCui,
                DisplayName = cached.DisplayName,
                AvailableForms = string.IsNullOrWhiteSpace(cached.RelatedFormsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(cached.RelatedFormsJson) ?? new List<string>(),
            };
        }
    }
}
