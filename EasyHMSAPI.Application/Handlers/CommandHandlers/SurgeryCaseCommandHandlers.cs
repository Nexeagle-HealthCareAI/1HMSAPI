using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SurgeryCaseCommandHandlers :
        IRequestHandler<RequestSurgeryRequestModel, RequestSurgeryResponseModel>,
        IRequestHandler<UpdateSurgeryCaseStatusRequestModel, UpdateSurgeryCaseStatusResponseModel>
    {
        // Fixed forward sequence. CANCELLED is valid from any key here (any non-terminal status).
        private static readonly Dictionary<string, string> ForwardTransition = new()
        {
            [IpdConstants.SurgeryStatus.Requested] = IpdConstants.SurgeryStatus.Scheduled,
            [IpdConstants.SurgeryStatus.Scheduled] = IpdConstants.SurgeryStatus.PreOp,
            [IpdConstants.SurgeryStatus.PreOp] = IpdConstants.SurgeryStatus.InTheatre,
            [IpdConstants.SurgeryStatus.InTheatre] = IpdConstants.SurgeryStatus.PostOp,
            [IpdConstants.SurgeryStatus.PostOp] = IpdConstants.SurgeryStatus.Completed,
        };

        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public SurgeryCaseCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<RequestSurgeryResponseModel> Handle(RequestSurgeryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProcedureName))
                    return new RequestSurgeryResponseModel { Success = false, Message = "HospitalId, AdmissionId, and ProcedureName are required." };

                var surgeryType = request.SurgeryType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(surgeryType) || !IpdConstants.SurgeryType.All.Contains(surgeryType))
                    return new RequestSurgeryResponseModel { Success = false, Message = "Invalid surgery type." };

                var urgency = request.Urgency?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(urgency) || !IpdConstants.SurgeryUrgency.All.Contains(urgency))
                    return new RequestSurgeryResponseModel { Success = false, Message = "Invalid urgency." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RequestSurgeryResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new RequestSurgeryResponseModel { Success = false, Message = "Admission is not active." };

                var now = DateTime.UtcNow;
                var surgeryCase = new SurgeryCase
                {
                    SurgeryCaseId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    ProcedureName = request.ProcedureName.Trim(),
                    SurgeryType = surgeryType,
                    Urgency = urgency,
                    RequestedBy = request.LoggedInUserName,
                    RequestedAt = now,
                    SurgeonDoctorId = request.SurgeonDoctorId,
                    SurgeonName = string.IsNullOrWhiteSpace(request.SurgeonName) ? null : request.SurgeonName.Trim(),
                    AnaesthetistDoctorId = request.AnaesthetistDoctorId,
                    AnaesthetistName = string.IsNullOrWhiteSpace(request.AnaesthetistName) ? null : request.AnaesthetistName.Trim(),
                    StatusCode = IpdConstants.SurgeryStatus.Requested,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.SurgeryCase.Add(surgeryCase);

                _context.SurgeryStatusHistory.Add(new SurgeryStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    SurgeryCaseId = surgeryCase.SurgeryCaseId,
                    FromStatus = null,
                    ToStatus = IpdConstants.SurgeryStatus.Requested,
                    ChangedAt = now,
                    ChangedBy = request.LoggedInUserName,
                });

                await _context.SaveChangesAsync(cancellationToken);

                return new RequestSurgeryResponseModel { Success = true, Message = "Surgery requested.", SurgeryCaseId = surgeryCase.SurgeryCaseId };
            }
            catch (Exception)
            {
                return new RequestSurgeryResponseModel { Success = false, Message = "Error requesting surgery." };
            }
        }

        public async Task<UpdateSurgeryCaseStatusResponseModel> Handle(UpdateSurgeryCaseStatusRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var toStatus = request.ToStatus?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(toStatus))
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = "ToStatus is required." };

                var surgeryCase = await _context.SurgeryCase
                    .FirstOrDefaultAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (surgeryCase == null)
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = "Surgery case not found." };

                if (IpdConstants.SurgeryStatus.Terminal.Contains(surgeryCase.StatusCode))
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = $"Case is already {surgeryCase.StatusCode.ToLowerInvariant()}." };

                var isValidCancel = toStatus == IpdConstants.SurgeryStatus.Cancelled;
                var isValidForward = ForwardTransition.TryGetValue(surgeryCase.StatusCode, out var expectedNext) && expectedNext == toStatus;
                if (!isValidCancel && !isValidForward)
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = $"Cannot move from {surgeryCase.StatusCode} to {toStatus}." };

                if (isValidCancel && string.IsNullOrWhiteSpace(request.Reason))
                    return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = "A reason is required to cancel a surgery case." };

                // Safety gate: sequence order alone isn't enough — a case can't leave PRE_OP without
                // a confirmed pre-op assessment and WHO Sign-In, and can't leave IN_THEATRE without
                // WHO Time-Out and Sign-Out. CANCELLED bypasses this (always allowed, reason required above).
                if (isValidForward)
                {
                    var missing = new List<string>();
                    if (toStatus == IpdConstants.SurgeryStatus.InTheatre)
                    {
                        var preOp = await _context.PreOpAssessment
                            .Where(a => a.SurgeryCaseId == surgeryCase.SurgeryCaseId)
                            .OrderByDescending(a => a.AssessedAt)
                            .FirstOrDefaultAsync(cancellationToken);
                        if (preOp == null)
                            missing.Add("pre-op assessment not recorded");
                        else
                        {
                            if (!preOp.NpoConfirmed) missing.Add("NPO not confirmed");
                            if (!preOp.AllergiesReviewed) missing.Add("allergies not reviewed");
                            if (!preOp.InvestigationsReviewed) missing.Add("investigations not reviewed");
                            if (!preOp.ConsentConfirmed) missing.Add("consent not confirmed");
                        }

                        var checklistForSignIn = await _context.SurgicalSafetyChecklist
                            .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCase.SurgeryCaseId, cancellationToken);
                        if (checklistForSignIn?.SignInCompletedAt == null)
                            missing.Add("WHO Sign-In not completed");
                    }
                    else if (toStatus == IpdConstants.SurgeryStatus.PostOp)
                    {
                        var checklistForTimeOut = await _context.SurgicalSafetyChecklist
                            .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCase.SurgeryCaseId, cancellationToken);
                        if (checklistForTimeOut?.TimeOutCompletedAt == null)
                            missing.Add("WHO Time-Out not completed");
                        if (checklistForTimeOut?.SignOutCompletedAt == null)
                            missing.Add("WHO Sign-Out not completed");
                    }

                    if (missing.Count > 0)
                    {
                        var stageLabel = toStatus == IpdConstants.SurgeryStatus.InTheatre ? "In Theatre" : "Post-Op";
                        return new UpdateSurgeryCaseStatusResponseModel
                        {
                            Success = false,
                            Message = $"Cannot move to {stageLabel}: {string.Join("; ", missing)}."
                        };
                    }
                }

                var now = DateTime.UtcNow;
                var fromStatus = surgeryCase.StatusCode;
                surgeryCase.StatusCode = toStatus;
                surgeryCase.UpdatedAt = now;
                surgeryCase.UpdatedBy = request.LoggedInUserName;
                if (isValidCancel)
                    surgeryCase.CancelledReason = request.Reason!.Trim();

                _context.SurgeryStatusHistory.Add(new SurgeryStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    SurgeryCaseId = surgeryCase.SurgeryCaseId,
                    FromStatus = fromStatus,
                    ToStatus = toStatus,
                    ChangedAt = now,
                    ChangedBy = request.LoggedInUserName,
                    Reason = request.Reason,
                });

                // Sync the case's active booking so the theatre frees up appropriately.
                var activeBooking = await _context.OTBooking.FirstOrDefaultAsync(
                    b => b.SurgeryCaseId == surgeryCase.SurgeryCaseId && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode), cancellationToken);
                var theatreJustCompleted = false;
                if (activeBooking != null)
                {
                    if (toStatus == IpdConstants.SurgeryStatus.InTheatre)
                    {
                        activeBooking.StatusCode = IpdConstants.OTBookingStatus.InProgress;
                        activeBooking.UpdatedAt = now;
                        activeBooking.UpdatedBy = request.LoggedInUserName;
                    }
                    else if (fromStatus == IpdConstants.SurgeryStatus.InTheatre)
                    {
                        var cancelled = toStatus == IpdConstants.SurgeryStatus.Cancelled;
                        activeBooking.StatusCode = cancelled ? IpdConstants.OTBookingStatus.Cancelled : IpdConstants.OTBookingStatus.Completed;
                        activeBooking.UpdatedAt = now;
                        activeBooking.UpdatedBy = request.LoggedInUserName;
                        theatreJustCompleted = !cancelled;
                    }
                    else if (toStatus == IpdConstants.SurgeryStatus.Cancelled)
                    {
                        activeBooking.StatusCode = IpdConstants.OTBookingStatus.Cancelled;
                        activeBooking.UpdatedAt = now;
                        activeBooking.UpdatedBy = request.LoggedInUserName;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Best-effort flat OT-usage charge, same "never block the clinical action" pattern as
                // RegisterAppointmentHandler's auto-OPD-consult-charge — a billing hiccup here must
                // never undo a status change that's already been recorded.
                if (theatreJustCompleted && activeBooking != null && surgeryCase.EncounterId.HasValue)
                {
                    try
                    {
                        var theatre = await _context.OperationTheatre
                            .FirstOrDefaultAsync(t => t.TheatreId == activeBooking.TheatreId, cancellationToken);
                        if (theatre != null && theatre.Price > 0)
                        {
                            await _mediator.Send(new AddChargeEventRequestModel
                            {
                                HospitalId = request.HospitalId,
                                PatientId = surgeryCase.PatientId,
                                EncounterId = surgeryCase.EncounterId.Value,
                                Charges = new List<ChargeDetail>
                                {
                                    new ChargeDetail
                                    {
                                        ChargeId = null,
                                        DisplayName = $"OT Usage - {theatre.TheatreName}",
                                        Qty = 1,
                                        Rate = theatre.Price,
                                        DiscountPercent = 0,
                                        CategoryCode = "OT",
                                        AttributedDoctorId = surgeryCase.SurgeonDoctorId,
                                    },
                                },
                                LoggedInUserName = request.LoggedInUserName,
                                LoggedInUserId = request.LoggedInUserId,
                            }, cancellationToken);
                        }
                    }
                    catch
                    {
                        // Non-fatal — the OT charge can still be added manually from billing.
                    }
                }

                return new UpdateSurgeryCaseStatusResponseModel { Success = true, Message = "Status updated." };
            }
            catch (Exception)
            {
                return new UpdateSurgeryCaseStatusResponseModel { Success = false, Message = "Error updating surgery case status." };
            }
        }
    }
}
