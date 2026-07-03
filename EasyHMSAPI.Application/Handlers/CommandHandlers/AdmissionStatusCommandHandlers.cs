using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Admission status transitions. Every change writes an AdmissionStatusHistory row (the KPI
    /// source for BOR/turnaround/discharge-TAT) and, when the new status is terminal, auto-releases
    /// the admission's current bed. DISCHARGED has its own handler so notes/timestamp are always
    /// captured; everything else (interim + other exits) goes through the generic transition.
    /// </summary>
    public class AdmissionStatusCommandHandlers :
        IRequestHandler<DischargeAdmissionRequestModel, DischargeAdmissionResponseModel>,
        IRequestHandler<UpdateAdmissionStatusRequestModel, UpdateAdmissionStatusResponseModel>
    {
        private static readonly string[] AllowedGenericTransitions =
        {
            IpdConstants.AdmissionStatus.DischargeInitiated,
            IpdConstants.AdmissionStatus.DischargeBilled,
            IpdConstants.AdmissionStatus.Lama,
            IpdConstants.AdmissionStatus.Dama,
            IpdConstants.AdmissionStatus.TransferredOut,
            IpdConstants.AdmissionStatus.Expired,
            IpdConstants.AdmissionStatus.Cancelled,
        };

        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;

        public AdmissionStatusCommandHandlers(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
        }

        public async Task<DischargeAdmissionResponseModel> Handle(DischargeAdmissionRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new DischargeAdmissionResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new DischargeAdmissionResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new DischargeAdmissionResponseModel { Success = false, Message = "Admission is already closed." };

                var now = DateTime.UtcNow;
                var dischargedAt = request.DischargedAt ?? now;

                _context.AdmissionStatusHistory.Add(new AdmissionStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    FromStatus = admission.StatusCode,
                    ToStatus = IpdConstants.AdmissionStatus.Discharged,
                    ChangedAt = now,
                    ChangedBy = request.LoggedInUserName,
                    Reason = "Discharged",
                });

                admission.StatusCode = IpdConstants.AdmissionStatus.Discharged;
                admission.DischargedAt = dischargedAt;
                admission.DischargedBy = request.LoggedInUserName;
                if (!string.IsNullOrWhiteSpace(request.DischargeNotes)) admission.DischargeNotes = request.DischargeNotes.Trim();
                admission.UpdatedAt = now;
                admission.UpdatedBy = request.LoggedInUserName;

                var bedReleased = await ReleaseActiveBedAsync(admission.AdmissionId, request.LoggedInUserName, now, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                await SendDischargeNotificationAsync(admission, dischargedAt, cancellationToken);

                return new DischargeAdmissionResponseModel
                {
                    Success = true,
                    Message = "Patient discharged.",
                    AdmissionId = admission.AdmissionId,
                    DischargedAt = dischargedAt,
                    BedReleased = bedReleased,
                };
            }
            catch (Exception)
            {
                return new DischargeAdmissionResponseModel { Success = false, Message = "Error discharging patient." };
            }
        }

        public async Task<UpdateAdmissionStatusResponseModel> Handle(UpdateAdmissionStatusRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ToStatus))
                    return new UpdateAdmissionStatusResponseModel { Success = false, Message = "HospitalId, AdmissionId and ToStatus are required." };

                var toStatus = request.ToStatus.Trim().ToUpperInvariant();
                if (!AllowedGenericTransitions.Contains(toStatus))
                    return new UpdateAdmissionStatusResponseModel { Success = false, Message = "Invalid status. Use the discharge endpoint for DISCHARGED." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new UpdateAdmissionStatusResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new UpdateAdmissionStatusResponseModel { Success = false, Message = "Admission is already closed." };

                var now = DateTime.UtcNow;
                _context.AdmissionStatusHistory.Add(new AdmissionStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    FromStatus = admission.StatusCode,
                    ToStatus = toStatus,
                    ChangedAt = now,
                    ChangedBy = request.LoggedInUserName,
                    Reason = request.Reason,
                });

                admission.StatusCode = toStatus;
                admission.UpdatedAt = now;
                admission.UpdatedBy = request.LoggedInUserName;

                var bedReleased = false;
                if (IpdConstants.AdmissionStatus.Terminal.Contains(toStatus))
                {
                    admission.DischargedAt ??= now;
                    admission.DischargedBy ??= request.LoggedInUserName;
                    bedReleased = await ReleaseActiveBedAsync(admission.AdmissionId, request.LoggedInUserName, now, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new UpdateAdmissionStatusResponseModel
                {
                    Success = true,
                    Message = $"Admission status updated to {toStatus}.",
                    AdmissionId = admission.AdmissionId,
                    StatusCode = toStatus,
                    BedReleased = bedReleased,
                };
            }
            catch (Exception)
            {
                return new UpdateAdmissionStatusResponseModel { Success = false, Message = "Error updating admission status." };
            }
        }

        // Fires a plain-text discharge notice on SMS + WhatsApp. Only DISCHARGED sends this — a
        // "you've been discharged" message is wrong for LAMA/DAMA/TRANSFERRED_OUT/EXPIRED/
        // CANCELLED, so UpdateAdmissionStatusHandler deliberately does not call this. Never lets a
        // notification failure fail the discharge itself — matches the messaging services' own
        // internal catch-and-return-false posture.
        private async Task SendDischargeNotificationAsync(Admission admission, DateTime dischargedAt, CancellationToken cancellationToken)
        {
            try
            {
                var mobile = await _context.PatientRegistrations
                    .Where(p => p.HospitalId == admission.HospitalId && p.PatientId == admission.PatientId)
                    .Select(p => p.Mobile)
                    .FirstOrDefaultAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(mobile)) return;

                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == admission.HospitalId)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? "the hospital";

                var patientName = await _context.PatientRegistrations
                    .Where(p => p.HospitalId == admission.HospitalId && p.PatientId == admission.PatientId)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Patient";

                var dateLabel = dischargedAt.ToString("dd MMM yyyy");
                var message = $"Dear {patientName}, you have been discharged from {hospitalName} on {dateLabel}. Wishing you a speedy recovery.";

                await _smsService.SendInvitationSmsAsync(mobile, message);
                await _whatsAppMessagingService.SendDischargeNotificationAsync(mobile, patientName, hospitalName, dateLabel);
            }
            catch (Exception)
            {
                // Notification failure must never block/roll back a discharge that already succeeded.
            }
        }

        // Releases the admission's ACTIVE bed assignment, if any. Caller still owns SaveChangesAsync.
        private async Task<bool> ReleaseActiveBedAsync(Guid admissionId, string? releasedBy, DateTime now, CancellationToken cancellationToken)
        {
            var activeBed = await _context.BedAssignment
                .FirstOrDefaultAsync(a => a.AdmissionId == admissionId && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
            if (activeBed == null) return false;

            activeBed.StatusCode = IpdConstants.BedAssignmentStatus.Released;
            activeBed.ReleasedAt = now;
            activeBed.ReleasedBy = releasedBy;
            activeBed.UpdatedAt = now;
            activeBed.UpdatedBy = releasedBy;
            return true;
        }
    }
}
