using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RegisterAppointmentHandler : IRequestHandler<RegisterAppointmentRequestModel, RegisterAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IMediator _mediator;

        public RegisterAppointmentHandler(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService, IMediator mediator)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _mediator = mediator;
        }

        public async Task<RegisterAppointmentResponseModel> Handle(RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Check doctor status before proceeding
                var existingDoctor = await _context.Doctors
                    .Where(d => d.DoctorID == request.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked)
                    .Select(x => new
                    {
                        x.User.UserStatusId,
                        x.DoctorID,
                        DoctorName = x.User.UserProfiles.FirstOrDefault()!.FullName
                    })
                    .FirstOrDefaultAsync();
                if (existingDoctor is null)
                {
                    throw new Exception("Doctor is not active or has been revoked.");
                }

                var patient = new PatientRegistration();
                if (request.Patient is not null)
                {
                    patient = await AddOrUpdatePatient(request, cancellationToken);
                }

                // Set status to 'Future' if appointment date is in the future
                var status = AppointmentBookingHelpers.ResolveInitialStatus(request.ApptDate);

                var (appointment, isNewAppointment) = await CreateOrUpdateAppointment(request, patient, status, cancellationToken);

                (bool billRefunded, decimal refundAmount, string? refundReceiptNo) refundResult = (false, 0m, null);
                if (!isNewAppointment && request.VoidExistingChargesAndRefund)
                {
                    refundResult = await VoidExistingChargesAndRefundAsync(appointment, cancellationToken);
                }

                if(request.AppointmentId is not null)
                {
                    patient = await _context.PatientRegistrations
                        .Where(p => p.PatientId == appointment.PatientId)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                // Determine appointment type based on patient history and prescription settings
                AppointmentTypeResolver.Result? typeResult = null;
                if(patient is not null)
                {
                    typeResult = await SetAppointmentType(appointment, patient, request, isNewAppointment, cancellationToken);
                }
                else
                {
                    throw new Exception("Patient not found for setting appointment type.");
                }

                // Save appointment first to ensure ApptId exists in DB
                await _context.SaveChangesAsync(cancellationToken);

                // Auto OPD billing: when the visit is chargeable (New / Old-Fee) and the billing
                // policy's OPD consult trigger is AUTO, create the encounter + consult charge and a
                // DRAFT invoice in the ledger at booking time. Best-effort — never fail the booking.
                if (isNewAppointment && typeResult?.FeeApplies == true)
                {
                    await TryAutoCreateOpdInvoice(appointment, patient, request, cancellationToken);
                }

                int? tokenNumber = null;
                if (request.AllocateToken && isNewAppointment)
                {
                    tokenNumber = await AllocateAppointmentTokenWithLocking(request, appointment, cancellationToken);
                }
                else if(request.AppointmentId is not null)
                {
                    var existingToken = await _context.AppointmentTokens
                        .Where(x => x.ApptId == request.AppointmentId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if(existingToken is not null) 
                    {
                        _context.Remove(existingToken); 
                    }
                    tokenNumber = await AllocateAppointmentTokenWithLocking(request, appointment, cancellationToken);
                }
                else if (request.AllocateToken && !isNewAppointment)
                {
                    // Get existing token number if appointment is being updated
                    var existingToken = await _context.AppointmentTokens
                        .FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId &&
                                                 t.DoctorId == request.DoctorId &&
                                                 t.TokenDate == request.ApptDate.Date &&
                                                 t.HospitalId == request.HospitalId,
                                                 cancellationToken);
                    if (existingToken != null)
                        tokenNumber = existingToken.TokenNo;
                }

                // Send SMS reminder
                //bool isSmsSent = false;
                bool isReminderSent = false;
                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment is booked for {appointment.ApptDate:yyyy-MM-dd} at {appointment.StartAt:HH:mm}.";
                    var token = string.Empty;
                    if (tokenNumber.HasValue && tokenNumber.Value > 0)
                    {
                        var groupIndex = (tokenNumber.Value - 1) / 30;
                        var prefix = (char)(65 + groupIndex);
                        var num = ((tokenNumber.Value - 1) % 30) + 1;
                        token = $"{prefix}-{num}";
                        smsMsg += $" Your token number is {token}.";
                    }
                    else if (tokenNumber.HasValue)
                    {
                        token = tokenNumber.Value.ToString();
                        smsMsg += $" Your token number is {token}.";
                    }
                    //isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);

                    var hospitalName = await _context.Hospitals
                        .Where(h => h.HospitalID == request.HospitalId)
                        .Select(h => h.Name)
                        .FirstOrDefaultAsync(cancellationToken);
                    var doctorName = existingDoctor.DoctorName;
                    var appointmentDate = appointment.ApptDate.Date.ToString("dd-MM-yyyy");
                    var appointmentTime = appointment.StartAt.ToString("HH:mm");
                    isReminderSent = await _whatsAppMessagingService.SendAppointmentConfirmationAsync(
                        patient.Mobile,
                        patient.FullName ?? string.Empty,
                        hospitalName ?? string.Empty,
                        doctorName,
                        token,
                        appointmentDate,
                        appointmentTime);
                }

                var message = "Appointment registered successfully";
                if (refundResult.billRefunded)
                    message += $". ₹{refundResult.refundAmount:0.##} was refunded (receipt {refundResult.refundReceiptNo}) and the prior consultation bill was voided.";

                return new RegisterAppointmentResponseModel
                {
                    PatientId = patient.PatientId,
                    AppointmentId = appointment.ApptId,
                    Status = status,
                    TokenNumber = tokenNumber,
                    IsReminderSent = isReminderSent,
                    Message = message,
                    BillRefunded = refundResult.billRefunded,
                    RefundAmount = refundResult.billRefunded ? refundResult.refundAmount : null,
                    RefundReceiptNo = refundResult.refundReceiptNo
                };
            }
            catch (DbUpdateException dbEx)
            {
                var msg = "Failed to register appointment, Db Exception" + dbEx + dbEx.InnerException + dbEx.StackTrace;
                throw new Exception(msg);
            }
            catch (Exception ex)
            {
                var msg = "Failed to register appointment" + ex + ex.InnerException + ex.StackTrace;
                throw new Exception(msg);
            }
        }

        private Task<PatientRegistration> AddOrUpdatePatient(RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            return AppointmentBookingHelpers.FindOrCreatePatientAsync(_context, request.Patient, request.HospitalId, request.UserId, cancellationToken);
        }

        private async Task<(Appointment appointment, bool isNewAppointment)> CreateOrUpdateAppointment(RegisterAppointmentRequestModel request, PatientRegistration patient, string statusCode, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.UserId);

            bool isNew = false;
            if (request.AppointmentId is not null)
            {
                var existingAppointment = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingAppointment is not null)
                {
                    if(existingAppointment.DoctorId != request.DoctorId)
                    {
                        existingAppointment.DoctorId = request.DoctorId;
                    }
                    if(request.ReferredByReferrerId is not null)
                    {
                        existingAppointment.ReferredByReferrerId = request.ReferredByReferrerId;
                        existingAppointment.ReferrerRelation = string.IsNullOrWhiteSpace(request.ReferrerRelation) ? null : request.ReferrerRelation;
                    }
                    if (!string.IsNullOrWhiteSpace(request.Reason))
                    {
                        existingAppointment.Reason = request.Reason;
                    }
                    if (!string.IsNullOrWhiteSpace(request.Patient?.InsuranceId))
                    {
                        existingAppointment.InsuranceId = request.Patient.InsuranceId;
                    }
                    if (!string.IsNullOrWhiteSpace(request.Patient?.PaymentMode))
                    {
                        existingAppointment.PaymentMode = request.Patient.PaymentMode;
                    }
                    if(existingAppointment.ApptDate.Date != request.ApptDate.Date)
                    {
                        existingAppointment.ApptDate = request.ApptDate;
                        var newStatus = AppointmentBookingHelpers.ResolveInitialStatus(request.ApptDate);
                        existingAppointment.CurrentStatusCode = newStatus;
                        existingAppointment.LastStatusCodeAt = DateTime.UtcNow;
                        var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                        ? new List<object>()
                        : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                        history.Add(new { status = newStatus, timestamp = DateTime.UtcNow });
                        existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                    }

                    // A caller that explicitly picks a slot (the full edit form) is honored directly,
                    // no auto-search. A caller that doesn't (RescheduleDialog's date-only reschedule)
                    // keeps the original auto-pick-first-available behavior below, unchanged.
                    if (request.StartAt.HasValue)
                    {
                        var explicitDuration = request.SlotTimeInMinutes.HasValue && request.SlotTimeInMinutes.Value > 0 ? request.SlotTimeInMinutes.Value : 15;
                        existingAppointment.StartAt = request.StartAt.Value;
                        existingAppointment.EndAt = request.StartAt.Value.AddMinutes(explicitDuration);
                    }
                    else
                    {
                        var bookedSlots = await (from a in _context.Appointments
                                                 join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                                 join u in _context.Users on d.UserID equals u.UserID
                                                 where a.DoctorId == request.DoctorId && a.HospitalId == request.HospitalId && a.ApptDate.Date == request.ApptDate.Date && u.UserStatusId != (int)UserStatusEnum.Revoked && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                                                 select a.StartAt.TimeOfDay)
                                         .ToListAsync(cancellationToken);

                        var requestDate = request.ApptDate.Date;

                        var overrideShifts = await _context.DoctorShiftOverrides
                            .Where(o => o.DoctorID == request.DoctorId &&
                                      o.HospitalId == request.HospitalId &&
                                      o.StartDate <= requestDate &&
                                      (!o.EndDate.HasValue || o.EndDate >= requestDate))
                            .OrderBy(o => o.StartTime)
                            .ToListAsync(cancellationToken);

                        var shiftDetails = new List<ShiftDayDetailsModel>();
                        if (overrideShifts.Count > 0)
                        {
                            shiftDetails = overrideShifts
                                .Select(shift => new ShiftDayDetailsModel
                                {
                                    OverrideId = shift.OverrideID,
                                    ShiftName = shift.ShiftName,
                                    StartTime = shift.StartTime,
                                    EndTime = shift.EndTime,
                                    SlotDurationInMinutes = shift.SlotDurationInMinutes,
                                    RecurringDays = shift.RecurringDays
                                })
                                .ToList();
                        }
                        else
                        {
                            shiftDetails = await _context.DoctorShiftTemplates
                                .Where(t => t.IsActive)
                                .OrderBy(t => t.StartTime)
                                .Select(t => new ShiftDayDetailsModel
                                {
                                    ShiftName = t.ShiftName,
                                    StartTime = t.StartTime,
                                    EndTime = t.EndTime,
                                    SlotDurationInMinutes = t.SlotDurationInMinutes
                                })
                                .ToListAsync(cancellationToken);
                        }

                        int slotDurationMinutes = shiftDetails.FirstOrDefault()?.SlotDurationInMinutes ?? 10;
                        var availableSlotStart = FindFirstAvailableSlot(bookedSlots, shiftDetails, slotDurationMinutes, request.ApptDate);

                        if (availableSlotStart.HasValue)
                        {
                            existingAppointment.StartAt = availableSlotStart.Value;
                            request.StartAt = availableSlotStart.Value;
                            existingAppointment.EndAt = availableSlotStart.Value.AddMinutes(slotDurationMinutes);
                        }
                    }

                    return (existingAppointment, isNew);
                }
                else
                {
                    throw new ArgumentException("Appointment not found for the given AppointmentId");
                }
            }
            else
            {
                if (request.StartAt is null)
                    throw new ArgumentNullException(nameof(request.StartAt));
                if(patient.PatientId is null)
                    throw new Exception("PatientId cannot be null when creating an appointment");

                var appointment = new Appointment
                {
                    ApptId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    PatientId = patient.PatientId,
                    ApptDate = request.ApptDate,
                    StartAt = request.StartAt.Value,
                    EndAt = request.StartAt.Value
                            .AddMinutes((request.SlotTimeInMinutes ?? 15) > 0 ? (request.SlotTimeInMinutes ?? 15) : 15),
                    CurrentStatusCode = statusCode,
                    Reason = request.Reason ?? string.Empty,
                    InsuranceId = !string.IsNullOrWhiteSpace(request?.Patient?.InsuranceId) ? request.Patient?.InsuranceId : null,
                    PaymentMode = !string.IsNullOrWhiteSpace(request?.Patient?.PaymentMode) ? request.Patient?.PaymentMode : "CASH",
                    StatusHistoryJson = $"[{{\"status\":\"{statusCode}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                    LastStatusCodeAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request?.UserId,
                    AppointmentType = null,
                    ReferredByReferrerId = request?.ReferredByReferrerId,
                    ReferrerRelation = string.IsNullOrWhiteSpace(request?.ReferrerRelation) ? null : request!.ReferrerRelation
                };
                _context.Appointments.Add(appointment);
                isNew = true;

                return (appointment, isNew);
            }
        }

        private async Task<AppointmentTypeResolver.Result> SetAppointmentType(Appointment appointment, PatientRegistration patient, RegisterAppointmentRequestModel request, bool isNewAppointment, CancellationToken cancellationToken)
        {
            // Delegated to the shared resolver so the booking decision and the consult-timeline
            // preview always agree. Inputs mirror the previous inline logic exactly.
            var result = await AppointmentTypeResolver.ResolveAsync(
                _context,
                request.Patient?.PatientId,
                patient.PatientId,
                request.Patient?.FullName,
                request.DoctorId,
                appointment.ApptDate,
                request.AppointmentId,
                cancellationToken);

            appointment.AppointmentType = result.AppointmentType;
            appointment.ValidUptoDate = result.ValidUptoDate;
            return result;
        }

        // Creates the OPD encounter + consult charge (CreateChargeEventHandler self-gates on the
        // AUTO policy and skips Old/No-Fee), then a DRAFT invoice for the posted charge. Best-effort:
        // any failure (e.g. number series not configured) is swallowed so the booking still succeeds.
        private async Task TryAutoCreateOpdInvoice(Appointment appointment, PatientRegistration patient, RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(patient.PatientId)) return;

                var policy = await _context.BillingPolicy
                    .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                if (!string.Equals(policy?.OpdConsultTrigger, "AUTO", StringComparison.OrdinalIgnoreCase))
                    return;

                var charge = await _mediator.Send(new CreateChargeEventRequestModel
                {
                    PatientId = patient.PatientId,
                    HospitalId = request.HospitalId,
                    EncounterType = "OPD",
                    AppointmentId = appointment.ApptId,
                    LoggedInUserName = request.UserId?.ToString(),
                }, cancellationToken);

                var data = charge?.Data;
                var hasCharge = data != null && (data.ConsultChargePosted || data.ConsultAlreadyCharged) && data.ConsultFee > 0;
                if (charge?.Success != true || data == null || !hasCharge)
                    return;

                await _mediator.Send(new CreateDraftInvoiceRequestModel
                {
                    PatientId = patient.PatientId,
                    HospitalId = request.HospitalId,
                    EncounterId = data.EncounterId,
                    LoggedInUserName = request.UserId?.ToString(),
                }, cancellationToken);
            }
            catch
            {
                // Non-fatal: the bill can still be created later from the Add Bill popup.
            }
        }

        // Reschedule opt-in: void this visit's posted charges/invoice and auto-refund whatever was
        // already collected — mirrors CancelAppointmentHandler's void-then-refund logic, except the
        // encounter itself is left Open (the visit continues on the new date, not torn down).
        private async Task<(bool billRefunded, decimal refundAmount, string? refundReceiptNo)> VoidExistingChargesAndRefundAsync(
            Appointment appointment, CancellationToken cancellationToken)
        {
            var billEncounter = await _context.Encounter
                .FirstOrDefaultAsync(e => e.SourceType == "Appointments"
                                       && e.SourceId == appointment.ApptId
                                       && e.StatusCode != BillingConstants.EncounterStatus.Cancelled,
                                       cancellationToken);
            if (billEncounter == null)
                return (false, 0m, null);

            var now = DateTime.UtcNow;

            var paidTotal = await _context.BillingPayment
                .Where(p => p.EncounterId == billEncounter.EncounterId
                         && p.PaymentType == BillingConstants.PaymentType.Payment)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var refundedAlready = await _context.BillingPayment
                .Where(p => p.EncounterId == billEncounter.EncounterId
                         && p.PaymentType == BillingConstants.PaymentType.Refund)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var netCollected = paidTotal - refundedAlready;

            bool billRefunded = false;
            decimal refundAmount = 0m;
            string? refundReceiptNo = null;

            if (netCollected > 0)
            {
                var lastMode = await _context.BillingPayment
                    .Where(p => p.EncounterId == billEncounter.EncounterId
                             && p.PaymentType == BillingConstants.PaymentType.Payment)
                    .OrderByDescending(p => p.PaidAt)
                    .Select(p => p.PaymentMode)
                    .FirstOrDefaultAsync(cancellationToken);

                var rcptSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, appointment.HospitalId, BillingConstants.NumberSeriesCode.Receipt, "SYSTEM", cancellationToken);
                rcptSeries.CurrentValue++;
                refundReceiptNo = NumberSeriesFormatter.Format(
                    rcptSeries.Prefix, rcptSeries.YearFormat, rcptSeries.Separator, rcptSeries.PadLength, rcptSeries.CurrentValue);
                rcptSeries.UpdatedAt = now;
                rcptSeries.UpdatedBy = "SYSTEM";

                _context.BillingPayment.Add(new BillingPayment
                {
                    PaymentId = Guid.NewGuid(),
                    HospitalId = appointment.HospitalId,
                    PatientId = billEncounter.PatientId,
                    EncounterId = billEncounter.EncounterId,
                    ReceiptNo = refundReceiptNo,
                    PaymentType = BillingConstants.PaymentType.Refund,
                    PaymentMode = lastMode,
                    PaymentDescription = "Auto refund — appointment rescheduled",
                    Amount = netCollected,
                    PaidAt = now,
                    CreatedAt = now,
                    CreatedBy = "SYSTEM",
                    UpdatedAt = now,
                    UpdatedBy = "SYSTEM"
                });

                var allocations = await _context.BillingPaymentAllocation
                    .Where(a => a.EncounterId == billEncounter.EncounterId)
                    .ToListAsync(cancellationToken);
                if (allocations.Count > 0)
                {
                    var allocationIds = allocations.Select(a => a.AllocationId).ToList();
                    var allocationCharges = await _context.BillingPaymentAllocationCharge
                        .Where(ac => allocationIds.Contains(ac.AllocationId))
                        .ToListAsync(cancellationToken);
                    if (allocationCharges.Count > 0)
                        _context.BillingPaymentAllocationCharge.RemoveRange(allocationCharges);
                    _context.BillingPaymentAllocation.RemoveRange(allocations);
                }

                billRefunded = true;
                refundAmount = netCollected;
            }

            var charges = await _context.BillingChargeEvent
                .Where(c => c.EncounterId == billEncounter.EncounterId
                         && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                .ToListAsync(cancellationToken);
            foreach (var c in charges)
            {
                c.StatusCode = BillingConstants.ChargeEventStatus.Void;
                c.VoidedAt = now;
                c.VoidedBy = "SYSTEM";
                c.VoidReason = "Appointment rescheduled";
                c.UpdatedAt = now;
                c.UpdatedBy = "SYSTEM";
            }

            var invoices = await _context.BillingInvoice
                .Where(i => i.EncounterId == billEncounter.EncounterId
                         && i.StatusCode != BillingConstants.InvoiceStatus.Cancelled)
                .ToListAsync(cancellationToken);
            foreach (var inv in invoices)
            {
                inv.StatusCode = BillingConstants.InvoiceStatus.Cancelled;
                inv.UpdatedAt = now;
                inv.UpdatedBy = "SYSTEM";
            }

            return (billRefunded, refundAmount, refundReceiptNo);
        }

        private static DateTime? FindFirstAvailableSlot(List<TimeSpan> bookedSlots, List<ShiftDayDetailsModel> shiftDetails, int slotDurationMinutes, DateTime appointmentDate)
        {
            foreach (var shift in shiftDetails)
            {
                var currentSlotStart = shift.StartTime ?? TimeSpan.Zero;
                var shiftEndTime = shift.EndTime ?? TimeSpan.Zero;
                
                while (currentSlotStart.Add(TimeSpan.FromMinutes(slotDurationMinutes)) <= shiftEndTime)
                {
                    if (!bookedSlots.Contains(currentSlotStart))
                    {
                        return appointmentDate.Date.Add(currentSlotStart);
                    }
                    currentSlotStart = currentSlotStart.Add(TimeSpan.FromMinutes(slotDurationMinutes));
                }
            }
            
            return null;
        }

        private Task<int?> AllocateAppointmentTokenWithLocking(RegisterAppointmentRequestModel request, Appointment appointment, CancellationToken cancellationToken)
        {
            return AppointmentBookingHelpers.AllocateTokenWithLockingAsync(_context, request.HospitalId, request.DoctorId, request.ApptDate, appointment.ApptId, cancellationToken);
        }
    }
}
