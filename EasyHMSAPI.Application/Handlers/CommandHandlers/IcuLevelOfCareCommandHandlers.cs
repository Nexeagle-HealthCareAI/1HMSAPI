using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class IcuLevelOfCareCommandHandlers : IRequestHandler<RecordLevelOfCareRequestModel, RecordLevelOfCareResponseModel>
    {
        private readonly AppDbContext _context;

        public IcuLevelOfCareCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordLevelOfCareResponseModel> Handle(RecordLevelOfCareRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordLevelOfCareResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var level = request.Level?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(level) || !IpdConstants.IcuLevelOfCare.All.Contains(level))
                    return new RecordLevelOfCareResponseModel { Success = false, Message = "Invalid level of care." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordLevelOfCareResponseModel { Success = false, Message = "Admission not found." };

                var now = DateTime.UtcNow;
                var record = new IcuLevelOfCare
                {
                    IcuLevelOfCareId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    Level = level,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    AssessedBy = request.LoggedInUserName ?? "Unknown",
                    AssessedAt = now,
                };
                _context.IcuLevelOfCare.Add(record);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordLevelOfCareResponseModel { Success = true, Message = "Level of care recorded.", IcuLevelOfCareId = record.IcuLevelOfCareId };
            }
            catch (Exception)
            {
                return new RecordLevelOfCareResponseModel { Success = false, Message = "Error recording level of care." };
            }
        }
    }
}
