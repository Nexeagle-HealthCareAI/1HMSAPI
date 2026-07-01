using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Records one intake/output entry. Pure insert, no transaction.</summary>
    public class FluidEntryCommandHandlers : IRequestHandler<RecordFluidEntryRequestModel, RecordFluidEntryResponseModel>
    {
        private readonly AppDbContext _context;

        public FluidEntryCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordFluidEntryResponseModel> Handle(RecordFluidEntryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordFluidEntryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var direction = request.Direction?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(direction) || !IpdConstants.FluidDirection.All.Contains(direction))
                    return new RecordFluidEntryResponseModel { Success = false, Message = "Invalid direction." };

                if (string.IsNullOrWhiteSpace(request.Subtype))
                    return new RecordFluidEntryResponseModel { Success = false, Message = "Subtype is required." };

                if (request.VolumeMl <= 0 || request.VolumeMl > 20000)
                    return new RecordFluidEntryResponseModel { Success = false, Message = "Volume must be between 0 and 20000 mL." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordFluidEntryResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RecordFluidEntryResponseModel { Success = false, Message = "Admission is not active." };

                var now = DateTime.UtcNow;
                var entry = new FluidEntry
                {
                    FluidEntryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    Direction = direction,
                    Subtype = request.Subtype.Trim(),
                    VolumeMl = request.VolumeMl,
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    RouteOrSite = string.IsNullOrWhiteSpace(request.RouteOrSite) ? null : request.RouteOrSite.Trim(),
                    Colour = string.IsNullOrWhiteSpace(request.Colour) ? null : request.Colour.Trim(),
                    RecordedAt = now,
                    RecordedBy = request.LoggedInUserName,
                    RecordedByUserId = request.LoggedInUserId,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.FluidEntry.Add(entry);

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordFluidEntryResponseModel { Success = true, Message = "Fluid entry recorded.", FluidEntryId = entry.FluidEntryId };
            }
            catch (Exception)
            {
                return new RecordFluidEntryResponseModel { Success = false, Message = "Error recording fluid entry." };
            }
        }
    }
}
