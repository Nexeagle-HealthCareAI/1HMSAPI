using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBloodBankInventoryHandler : IRequestHandler<GetBloodBankInventoryRequestModel, GetBloodBankInventoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBloodBankInventoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBloodBankInventoryResponseModel> Handle(GetBloodBankInventoryRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.BloodBag.Where(b => b.HospitalId == request.HospitalId);
            if (!string.IsNullOrWhiteSpace(request.Status))
                query = query.Where(b => b.Status == request.Status.Trim().ToUpperInvariant());

            var bags = await query
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BloodBankInventoryRow
                {
                    BloodBagId = b.BloodBagId,
                    BagNumber = b.BagNumber,
                    Component = b.Component,
                    BloodGroup = b.BloodGroup,
                    VolumeMl = b.VolumeMl,
                    CollectedAt = b.CollectedAt,
                    ExpiresAt = b.ExpiresAt,
                    StorageLocation = b.StorageLocation,
                    Status = b.Status,
                    ReservedForPatientId = b.ReservedForPatientId,
                    DiscardReason = b.DiscardReason,
                    DiscardedAt = b.DiscardedAt,
                })
                .ToListAsync(cancellationToken);

            if (bags.Count == 0)
                return new GetBloodBankInventoryResponseModel { Bags = bags };

            var patientIds = bags.Where(b => b.ReservedForPatientId != null).Select(b => b.ReservedForPatientId!).Distinct().ToList();
            var namesByPatientId = patientIds.Count == 0
                ? new System.Collections.Generic.Dictionary<string, string>()
                : await _context.PatientRegistrations
                    .Where(p => patientIds.Contains(p.PatientId))
                    .Select(p => new { p.PatientId, p.FullName })
                    .ToDictionaryAsync(p => p.PatientId, p => p.FullName, cancellationToken);

            foreach (var bag in bags)
            {
                if (bag.ReservedForPatientId != null && namesByPatientId.TryGetValue(bag.ReservedForPatientId, out var name))
                    bag.ReservedForPatientName = name;
            }

            return new GetBloodBankInventoryResponseModel { Bags = bags };
        }
    }
}
