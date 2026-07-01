using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Restraint orders — NABH requires a physician order, a monitoring interval, and family
    /// notification. ACTIVE/RELEASED lifecycle mirrors BedAssignment; only one ACTIVE restraint
    /// per admission at a time (backstopped by UX_RO_AdmissionActive, same pattern as
    /// BedAssignmentCommandHandlers catching the filtered-unique-index race).
    /// </summary>
    public class RestraintOrderCommandHandlers :
        IRequestHandler<StartRestraintRequestModel, StartRestraintResponseModel>,
        IRequestHandler<ReleaseRestraintRequestModel, ReleaseRestraintResponseModel>
    {
        private readonly AppDbContext _context;

        public RestraintOrderCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StartRestraintResponseModel> Handle(StartRestraintRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new StartRestraintResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (string.IsNullOrWhiteSpace(request.RestraintType) || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.OrderedByDoctorName))
                    return new StartRestraintResponseModel { Success = false, Message = "Restraint type, reason and ordering doctor are required." };
                if (request.MonitoringIntervalMins <= 0 || request.MonitoringIntervalMins > 240)
                    return new StartRestraintResponseModel { Success = false, Message = "Monitoring interval must be between 1 and 240 minutes." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new StartRestraintResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new StartRestraintResponseModel { Success = false, Message = "Admission is not active." };

                var alreadyActive = await _context.RestraintOrder
                    .AnyAsync(r => r.AdmissionId == admission.AdmissionId && r.HospitalId == request.HospitalId && r.StatusCode == IpdConstants.RestraintStatus.Active, cancellationToken);
                if (alreadyActive)
                    return new StartRestraintResponseModel { Success = false, Message = "This admission already has an active restraint order — release it before starting a new one." };

                if (request.RelatedConsentRecordId.HasValue)
                {
                    var consentBelongs = await _context.ConsentRecord
                        .AnyAsync(c => c.ConsentRecordId == request.RelatedConsentRecordId.Value && c.AdmissionId == admission.AdmissionId, cancellationToken);
                    if (!consentBelongs)
                        return new StartRestraintResponseModel { Success = false, Message = "The related consent record does not belong to this admission." };
                }

                var now = DateTime.UtcNow;
                var order = new RestraintOrder
                {
                    RestraintOrderId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    RestraintType = request.RestraintType.Trim(),
                    Reason = request.Reason.Trim(),
                    OrderedByDoctorId = request.OrderedByDoctorId,
                    OrderedByDoctorName = request.OrderedByDoctorName.Trim(),
                    OrderedAt = now,
                    StartedAt = now,
                    StartedBy = request.LoggedInUserName,
                    StartedByUserId = request.LoggedInUserId,
                    MonitoringIntervalMins = request.MonitoringIntervalMins,
                    FamilyNotified = request.FamilyNotified,
                    FamilyNotifiedAt = request.FamilyNotified ? now : null,
                    FamilyNotificationNotes = string.IsNullOrWhiteSpace(request.FamilyNotificationNotes) ? null : request.FamilyNotificationNotes.Trim(),
                    RelatedConsentRecordId = request.RelatedConsentRecordId,
                    StatusCode = IpdConstants.RestraintStatus.Active,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.RestraintOrder.Add(order);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Concurrency backstop: two requests racing past the AnyAsync check above hit
                    // UX_RO_AdmissionActive — same pattern as BedAssignmentCommandHandlers.
                    return new StartRestraintResponseModel { Success = false, Message = "This admission already has an active restraint order." };
                }

                return new StartRestraintResponseModel { Success = true, Message = "Restraint started.", RestraintOrderId = order.RestraintOrderId };
            }
            catch (Exception)
            {
                return new StartRestraintResponseModel { Success = false, Message = "Error starting restraint." };
            }
        }

        public async Task<ReleaseRestraintResponseModel> Handle(ReleaseRestraintRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.RestraintOrderId == Guid.Empty)
                    return new ReleaseRestraintResponseModel { Success = false, Message = "HospitalId and RestraintOrderId are required." };

                var order = await _context.RestraintOrder
                    .FirstOrDefaultAsync(r => r.RestraintOrderId == request.RestraintOrderId && r.HospitalId == request.HospitalId, cancellationToken);
                if (order == null)
                    return new ReleaseRestraintResponseModel { Success = false, Message = "Restraint order not found." };
                if (order.StatusCode != IpdConstants.RestraintStatus.Active)
                    return new ReleaseRestraintResponseModel { Success = false, Message = "This restraint order is already released." };

                var now = DateTime.UtcNow;
                order.StatusCode = IpdConstants.RestraintStatus.Released;
                order.ReleasedAt = now;
                order.ReleasedBy = request.LoggedInUserName;
                order.ReleasedByUserId = request.LoggedInUserId;
                order.ReleaseReason = string.IsNullOrWhiteSpace(request.ReleaseReason) ? null : request.ReleaseReason.Trim();
                order.UpdatedAt = now;
                order.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ReleaseRestraintResponseModel { Success = true, Message = "Restraint released.", RestraintOrderId = order.RestraintOrderId };
            }
            catch (Exception)
            {
                return new ReleaseRestraintResponseModel { Success = false, Message = "Error releasing restraint." };
            }
        }
    }
}
