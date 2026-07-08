using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// True hard delete — removes the BedMaster row outright. Only possible for a bed that has
    /// never appeared in BedAssignment (that table's FK has no cascade, so the database itself
    /// would reject the delete otherwise); any bed with assignment history must be deactivated
    /// instead (see UpsertBedMasterHandler/BulkDeleteBedMasterHandler), which preserves the
    /// admission/billing history it's tied to.
    /// </summary>
    public class HardDeleteBedMasterHandler : IRequestHandler<HardDeleteBedMasterRequestModel, HardDeleteBedMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public HardDeleteBedMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HardDeleteBedMasterResponseModel> Handle(HardDeleteBedMasterRequestModel request, CancellationToken cancellationToken)
        {
            var bed = await _context.BedMaster
                .FirstOrDefaultAsync(b => b.BedId == request.BedId && b.HospitalId == request.HospitalId, cancellationToken);

            if (bed == null)
                return new HardDeleteBedMasterResponseModel { Success = false, Message = "Bed not found." };

            if (bed.StatusCode == "OCCUPIED")
                return new HardDeleteBedMasterResponseModel { Success = false, Message = $"Bed {bed.BedCode} is currently occupied and can't be deleted." };

            var hasHistory = await _context.BedAssignment.AnyAsync(a => a.BedId == bed.BedId, cancellationToken);
            if (hasHistory)
                return new HardDeleteBedMasterResponseModel { Success = false, Message = $"Bed {bed.BedCode} has assignment history and can't be permanently deleted. Deactivate it instead." };

            _context.BedMaster.Remove(bed);
            await _context.SaveChangesAsync(cancellationToken);

            return new HardDeleteBedMasterResponseModel { Success = true, Message = $"Bed {bed.BedCode} permanently deleted." };
        }
    }
}
