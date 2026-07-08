using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Soft-deletes (IsActive = false) every requested bed that isn't currently occupied. Beds
    /// aren't hard-deleted here (or anywhere in this codebase) because BedAssignment.BedId has a
    /// real FK with no cascade — any bed ever assigned to a patient can't be removed from the
    /// table. OCCUPIED beds are rejected individually rather than failing the whole batch, so a
    /// mixed selection still deactivates everything it safely can.
    /// </summary>
    public class BulkDeleteBedMasterHandler : IRequestHandler<BulkDeleteBedMasterRequestModel, BulkDeleteBedMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public BulkDeleteBedMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BulkDeleteBedMasterResponseModel> Handle(BulkDeleteBedMasterRequestModel request, CancellationToken cancellationToken)
        {
            var response = new BulkDeleteBedMasterResponseModel();

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

            var now = DateTime.UtcNow;
            foreach (var bed in beds)
            {
                if (bed.StatusCode == "OCCUPIED")
                {
                    response.Blocked.Add(new BedDeleteFailure
                    {
                        BedId = bed.BedId,
                        BedCode = bed.BedCode,
                        Reason = "Bed is currently occupied.",
                    });
                    continue;
                }

                bed.IsActive = false;
                bed.UpdatedAt = now;
                bed.UpdatedBy = request.LoggedInUserName;
                response.Deactivated.Add(bed.BedId);
            }

            if (response.Deactivated.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            response.Success = true;
            response.Message = response.Blocked.Count == 0
                ? $"{response.Deactivated.Count} bed(s) deactivated."
                : $"{response.Deactivated.Count} bed(s) deactivated, {response.Blocked.Count} blocked.";

            return response;
        }
    }
}
