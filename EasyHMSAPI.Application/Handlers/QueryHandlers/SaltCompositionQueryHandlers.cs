using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class SaltCompositionQueryHandlers :
        IRequestHandler<GetMoleculesRequestModel, GetMoleculesResponseModel>,
        IRequestHandler<GetSaltCompositionsRequestModel, GetSaltCompositionsResponseModel>,
        IRequestHandler<GetSubstituteItemsRequestModel, GetSubstituteItemsResponseModel>
    {
        private readonly AppDbContext _context;

        public SaltCompositionQueryHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMoleculesResponseModel> Handle(GetMoleculesRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Molecule.AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(m => m.Name.Contains(request.Search));

            var molecules = await query.OrderBy(m => m.Name).Take(100)
                .Select(m => new MoleculeDataModel { MoleculeId = m.MoleculeId, Name = m.Name })
                .ToListAsync(cancellationToken);
            return new GetMoleculesResponseModel { Molecules = molecules };
        }

        public async Task<GetSaltCompositionsResponseModel> Handle(GetSaltCompositionsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.SaltComposition.AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(s => s.DisplayName.Contains(request.Search));

            var compositions = await query.OrderBy(s => s.DisplayName).Take(100).ToListAsync(cancellationToken);
            var compositionIds = compositions.Select(c => c.SaltCompositionId).ToList();

            var components = await _context.SaltCompositionComponent
                .Where(c => compositionIds.Contains(c.SaltCompositionId))
                .ToListAsync(cancellationToken);
            var moleculeNames = await _context.Molecule
                .Where(m => components.Select(c => c.MoleculeId).Distinct().Contains(m.MoleculeId))
                .ToDictionaryAsync(m => m.MoleculeId, m => m.Name, cancellationToken);

            var result = compositions.Select(s => new SaltCompositionDataModel
            {
                SaltCompositionId = s.SaltCompositionId,
                DisplayName = s.DisplayName,
                DosageForm = s.DosageForm,
                Components = components.Where(c => c.SaltCompositionId == s.SaltCompositionId)
                    .Select(c => new SaltCompositionComponentDataModel
                    {
                        MoleculeId = c.MoleculeId,
                        MoleculeName = moleculeNames.TryGetValue(c.MoleculeId, out var n) ? n : "Unknown",
                        StrengthValue = c.StrengthValue,
                        StrengthUnit = c.StrengthUnit,
                    }).ToList(),
            }).ToList();

            return new GetSaltCompositionsResponseModel { Compositions = result };
        }

        public async Task<GetSubstituteItemsResponseModel> Handle(GetSubstituteItemsRequestModel request, CancellationToken cancellationToken)
        {
            var item = await _context.InventoryItem.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);

            if (item?.SaltCompositionId == null)
                return new GetSubstituteItemsResponseModel { HasComposition = false };

            var alternateItems = await _context.InventoryItem.AsNoTracking()
                .Where(i => i.HospitalId == request.HospitalId && i.SaltCompositionId == item.SaltCompositionId
                         && i.InventoryItemId != item.InventoryItemId && i.IsActive)
                .ToListAsync(cancellationToken);

            var stockByItem = new Dictionary<Guid, decimal>();
            if (alternateItems.Count > 0)
            {
                var altIds = alternateItems.Select(a => a.InventoryItemId).ToList();
                var stockQuery = _context.StockLevel.AsNoTracking().Where(sl => altIds.Contains(sl.InventoryItemId));
                if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                    stockQuery = stockQuery.Where(sl => sl.StoreId == request.StoreId);

                stockByItem = (await stockQuery
                        .GroupBy(sl => sl.InventoryItemId)
                        .Select(g => new { InventoryItemId = g.Key, Qty = g.Sum(x => x.QtyOnHand) })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.InventoryItemId, x => x.Qty);
            }

            var alternates = alternateItems
                .Select(a => new SubstituteItemDataModel
                {
                    InventoryItemId = a.InventoryItemId,
                    ItemName = a.ItemName,
                    Manufacturer = a.Manufacturer,
                    DefaultRate = a.DefaultRate,
                    StockAtStore = stockByItem.TryGetValue(a.InventoryItemId, out var qty) ? qty : 0,
                })
                .Where(a => a.StockAtStore > 0)
                .OrderBy(a => a.DefaultRate)
                .ToList();

            return new GetSubstituteItemsResponseModel { HasComposition = true, Alternates = alternates };
        }
    }
}
