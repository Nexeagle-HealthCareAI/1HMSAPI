using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// True hard delete for a batch of beds. Mirrors HardDeleteBedMasterHandler's rules (blocks
    /// OCCUPIED beds and any bed with BedAssignment history) but batches the lookups so an N-bed
    /// selection costs 2 queries instead of 2N. Beds that can't be deleted are reported per-bed
    /// rather than failing the whole batch.
    /// </summary>
    public class BulkHardDeleteBedMasterHandler : IRequestHandler<BulkHardDeleteBedMasterRequestModel, BulkHardDeleteBedMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public BulkHardDeleteBedMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BulkHardDeleteBedMasterResponseModel> Handle(BulkHardDeleteBedMasterRequestModel request, CancellationToken cancellationToken)
        {
            var response = new BulkHardDeleteBedMasterResponseModel();

            var ids = (request.BedIds ?? new List<Guid>()).Distinct().ToList();
            if (ids.Count == 0)
            {
                response.Success = false;
                response.Message = "No beds were selected.";
                return response;
            }

            var beds = await _context.BedMaster
                .Where(b => b.HospitalId == request.HospitalId && ids.Contains(b.BedId))
                .ToListAsync(cancellationToken);

            var foundIds = beds.Select(b => b.BedId).ToHashSet();
            foreach (var missingId in ids.Where(id => !foundIds.Contains(id)))
            {
                response.Blocked.Add(new BedDeleteFailure { BedId = missingId, Reason = "Bed not found." });
            }

            var assignedBedIds = await _context.BedAssignment
                .Where(a => foundIds.Contains(a.BedId))
                .Select(a => a.BedId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var assignedSet = assignedBedIds.ToHashSet();

            foreach (var bed in beds)
            {
                if (bed.StatusCode == "OCCUPIED")
                {
                    response.Blocked.Add(new BedDeleteFailure { BedId = bed.BedId, BedCode = bed.BedCode, Reason = "Bed is currently occupied." });
                    continue;
                }

                if (assignedSet.Contains(bed.BedId))
                {
                    response.Blocked.Add(new BedDeleteFailure { BedId = bed.BedId, BedCode = bed.BedCode, Reason = "Has assignment history — deactivate instead." });
                    continue;
                }

                _context.BedMaster.Remove(bed);
                response.Deleted.Add(bed.BedId);
            }

            if (response.Deleted.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            response.Success = true;
            response.Message = response.Blocked.Count == 0
                ? $"{response.Deleted.Count} bed(s) permanently deleted."
                : $"{response.Deleted.Count} bed(s) permanently deleted, {response.Blocked.Count} blocked.";

            return response;
        }
    }
}
