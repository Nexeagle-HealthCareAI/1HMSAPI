using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
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
        IRequestHandler<UpdateAdmissionStatusRequestModel, UpdateAdmissionStatusResponseModel>,
        IRequestHandler<ConfirmPatientArrivalRequestModel, ConfirmPatientArrivalResponseModel>,
        IRequestHandler<UpdateAdmissionDetailsRequestModel, UpdateAdmissionDetailsResponseModel>,
        IRequestHandler<UpsertAdmissionCoverageRequestModel, UpsertAdmissionCoverageResponseModel>
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
        private readonly IMediator _mediator;

        public AdmissionStatusCommandHandlers(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService, IMediator mediator)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _mediator = mediator;
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

        public async Task<ConfirmPatientArrivalResponseModel> Handle(ConfirmPatientArrivalRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new ConfirmPatientArrivalResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var admission = await _context.Admission
                            .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                        if (admission == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ConfirmPatientArrivalResponseModel { Success = false, Message = "Admission not found." };
                        }
                        if (admission.StatusCode != IpdConstants.AdmissionStatus.PreAdmit)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ConfirmPatientArrivalResponseModel { Success = false, Message = "Admission is not a pending pre-registration." };
                        }

                        var now = DateTime.UtcNow;
                        _context.AdmissionStatusHistory.Add(new AdmissionStatusHistory
                        {
                            HistoryId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            AdmissionId = admission.AdmissionId,
                            FromStatus = admission.StatusCode,
                            ToStatus = IpdConstants.AdmissionStatus.Admitted,
                            ChangedAt = now,
                            ChangedBy = request.LoggedInUserName,
                            Reason = "Patient arrived",
                        });

                        admission.StatusCode = IpdConstants.AdmissionStatus.Admitted;
                        admission.AdmittedAt = now;
                        admission.UpdatedAt = now;
                        admission.UpdatedBy = request.LoggedInUserName;

                        await _context.SaveChangesAsync(cancellationToken);

                        Guid? bedId = null, bedAssignmentId = null;
                        if (request.BedId.HasValue)
                        {
                            var assignResponse = await _mediator.Send(new AssignBedRequestModel
                            {
                                HospitalId = request.HospitalId,
                                AdmissionId = admission.AdmissionId,
                                BedId = request.BedId.Value,
                                LoggedInUserName = request.LoggedInUserName,
                            }, cancellationToken);
                            if (!assignResponse.Success)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new ConfirmPatientArrivalResponseModel { Success = false, Message = assignResponse.Message ?? "Could not assign the bed." };
                            }
                            bedId = assignResponse.BedId;
                            bedAssignmentId = assignResponse.BedAssignmentId;
                        }

                        await tx.CommitAsync(cancellationToken);

                        return new ConfirmPatientArrivalResponseModel
                        {
                            Success = true,
                            Message = "Arrival confirmed. Patient is now admitted.",
                            AdmissionId = admission.AdmissionId,
                            AdmittedAt = admission.AdmittedAt,
                            BedId = bedId,
                            BedAssignmentId = bedAssignmentId,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new ConfirmPatientArrivalResponseModel { Success = false, Message = "Error confirming arrival." };
                    }
                });
            }
            catch (Exception)
            {
                return new ConfirmPatientArrivalResponseModel { Success = false, Message = "Error confirming arrival." };
            }
        }

        public async Task<UpdateAdmissionDetailsResponseModel> Handle(UpdateAdmissionDetailsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new UpdateAdmissionDetailsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new UpdateAdmissionDetailsResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new UpdateAdmissionDetailsResponseModel { Success = false, Message = "Admission is closed — its details can no longer be edited." };

                var now = DateTime.UtcNow;
                if (request.PrimaryDoctorId.HasValue && request.PrimaryDoctorId.Value != Guid.Empty)
                    await AdmissionDoctorAssignmentHelper.ChangeDoctorAsync(_context, admission, request.PrimaryDoctorId.Value, request.LoggedInUserName, now, cancellationToken);
                if (!string.IsNullOrWhiteSpace(request.AdmissionReason)) admission.AdmissionReason = request.AdmissionReason.Trim();
                if (!string.IsNullOrWhiteSpace(request.Diagnosis)) admission.Diagnosis = request.Diagnosis.Trim();
                if (request.ExpectedDischargeAt.HasValue) admission.ExpectedDischargeAt = request.ExpectedDischargeAt;
                if (request.DepositExpected.HasValue) admission.DepositExpected = request.DepositExpected;

                if (!string.IsNullOrWhiteSpace(request.PayerType))
                {
                    var payerType = request.PayerType.Trim().ToUpperInvariant();
                    if (!IpdConstants.PayerType.All.Contains(payerType))
                        return new UpdateAdmissionDetailsResponseModel { Success = false, Message = "Invalid payer type." };
                    admission.PayerType = payerType;
                }

                // Routed through the same helper the dedicated "Change referrer" action uses, so
                // both entry points write one consistent AdmissionReferrerAssignment history trail.
                // Only acts when ReferralSource is actually supplied — callers that don't touch the
                // referral section at all leave these fields untouched, same as before.
                if (!string.IsNullOrWhiteSpace(request.ReferralSource))
                {
                    var normalizedSource = request.ReferralSource.Trim().ToUpperInvariant();
                    var referrerId = normalizedSource == "SELF" ? null : request.ReferredByReferrerId;
                    var referrerName = normalizedSource == "SELF" ? null : request.ReferralName;
                    var referrerType = normalizedSource == "SELF" ? null : request.ReferrerType;
                    await AdmissionReferrerAssignmentHelper.ChangeReferrerAsync(
                        _context, admission, normalizedSource, referrerId, referrerName, referrerType, request.LoggedInUserName, now, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(request.ReferringFacilityName)) admission.ReferringFacilityName = request.ReferringFacilityName.Trim();
                if (!string.IsNullOrWhiteSpace(request.ReferringFacilityType)) admission.ReferringFacilityType = request.ReferringFacilityType.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(request.ReferringFacilityContact)) admission.ReferringFacilityContact = request.ReferringFacilityContact.Trim();

                admission.UpdatedAt = now;
                admission.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new UpdateAdmissionDetailsResponseModel { Success = true, Message = "Admission details updated.", AdmissionId = admission.AdmissionId };
            }
            catch (Exception)
            {
                return new UpdateAdmissionDetailsResponseModel { Success = false, Message = "Error updating admission details." };
            }
        }

        public async Task<UpsertAdmissionCoverageResponseModel> Handle(UpsertAdmissionCoverageRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new UpsertAdmissionCoverageResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new UpsertAdmissionCoverageResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new UpsertAdmissionCoverageResponseModel { Success = false, Message = "Admission is closed — its coverage details can no longer be edited." };

                var now = DateTime.UtcNow;
                var coverage = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (coverage == null)
                {
                    coverage = new AdmissionCoverage
                    {
                        CoverageId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        AdmissionId = admission.AdmissionId,
                        PayerType = admission.PayerType,
                        StatusCode = IpdConstants.CoverageStatus.Pending,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                    };
                    _context.AdmissionCoverage.Add(coverage);
                }

                if (!string.IsNullOrWhiteSpace(request.PayerName)) coverage.PayerName = request.PayerName.Trim();
                if (!string.IsNullOrWhiteSpace(request.PolicyOrBeneficiaryNo)) coverage.PolicyOrBeneficiaryNo = request.PolicyOrBeneficiaryNo.Trim();
                if (!string.IsNullOrWhiteSpace(request.PreAuthNo)) coverage.PreAuthNo = request.PreAuthNo.Trim();
                if (!string.IsNullOrWhiteSpace(request.PackageCode)) coverage.PackageCode = request.PackageCode.Trim();
                if (request.SanctionedAmount.HasValue) coverage.SanctionedAmount = request.SanctionedAmount;
                if (!string.IsNullOrWhiteSpace(request.EntitledRoomCategory)) coverage.EntitledRoomCategory = request.EntitledRoomCategory.Trim().ToUpperInvariant();

                coverage.UpdatedAt = now;
                coverage.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertAdmissionCoverageResponseModel { Success = true, Message = "Coverage details updated.", CoverageId = coverage.CoverageId };
            }
            catch (Exception)
            {
                return new UpsertAdmissionCoverageResponseModel { Success = false, Message = "Error updating coverage details." };
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
