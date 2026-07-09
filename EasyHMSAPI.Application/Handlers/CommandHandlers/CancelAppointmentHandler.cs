using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
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
    public class CancelAppointmentHandler : IRequestHandler<CancelAppointmentRequestModel, CancelAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public CancelAppointmentHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<CancelAppointmentResponseModel> Handle(CancelAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId
                                            && a.PatientId == request.PatientId
                                            && a.HospitalId == request.HospitalId, cancellationToken);

                if (appt == null)
                    return new CancelAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    return new CancelAppointmentResponseModel { Success = false, Message = "This appointment is already cancelled." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    return new CancelAppointmentResponseModel { Success = false, Message = "Cannot cancel a completed appointment." };

                bool billVoided = false;
                bool billRefunded = false;
                decimal refundAmount = 0m;
                string? refundReceiptNo = null;

                // Check doctor status before proceeding
                var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == appt.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                if (!doctorActive)
                {
                    return new CancelAppointmentResponseModel { Success = false, Message = "Doctor is not active or has been revoked." };
                }

                appt.CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled;
                appt.LastStatusCodeAt = DateTime.UtcNow;
                appt.CancelledAt = DateTime.UtcNow;
                appt.CancelledBy = request.LoggedInUserName;
                appt.CancellationReason = request.Reason;

                var history = string.IsNullOrEmpty(appt.StatusHistoryJson)
                    ? new List<object>()
                    : JsonSerializer.Deserialize<List<object>>(appt.StatusHistoryJson) ?? new List<object>();
                history.Add(new { status = AppConstants.AppointmentStatus_Cancelled, timestamp = DateTime.UtcNow.ToString("o"), reason = request.Reason });
                appt.StatusHistoryJson = JsonSerializer.Serialize(history);

                var token = await _context.AppointmentTokens
                    .Where(t => t.ApptId == appt.ApptId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (token != null)
                {
                    token.TokenNo = 0;
                    _context.AppointmentTokens.Update(token);
                }

                _context.Appointments.Update(appt);

                // ── Billing cleanup ────────────────────────────────────────────
                // Booking may have auto-created an OPD encounter with a posted consult
                // charge + draft invoice. On cancellation we always tear that bill down so
                // it doesn't linger in the ledger:
                //   • unpaid  → void the charge + cancel the draft invoice + encounter.
                //   • paid    → additionally auto-issue a REFUND receipt for the collected
                //               amount and drop the payment allocations, so collected/outstanding
                //               net to zero with a full paid → refunded audit trail.
                var billEncounter = await _context.Encounter
                    .FirstOrDefaultAsync(e => e.SourceType == "Appointments"
                                           && e.SourceId == appt.ApptId
                                           && e.StatusCode != BillingConstants.EncounterStatus.Cancelled,
                                           cancellationToken);

                if (billEncounter != null)
                {
                    var billNow = DateTime.UtcNow;

                    var paidTotal = await _context.BillingPayment
                        .Where(p => p.EncounterId == billEncounter.EncounterId
                                 && p.PaymentType == BillingConstants.PaymentType.Payment)
                        .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

                    var refundedAlready = await _context.BillingPayment
                        .Where(p => p.EncounterId == billEncounter.EncounterId
                                 && p.PaymentType == BillingConstants.PaymentType.Refund)
                        .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

                    var netCollected = paidTotal - refundedAlready;

                    // Auto-refund any net cash still held against this encounter.
                    if (netCollected > 0)
                    {
                        // Mirror the original payment mode where we can, for a sensible receipt.
                        var lastMode = await _context.BillingPayment
                            .Where(p => p.EncounterId == billEncounter.EncounterId
                                     && p.PaymentType == BillingConstants.PaymentType.Payment)
                            .OrderByDescending(p => p.PaidAt)
                            .Select(p => p.PaymentMode)
                            .FirstOrDefaultAsync(cancellationToken);

                        var rcptSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                            _context, appt.HospitalId, BillingConstants.NumberSeriesCode.Receipt, "SYSTEM", cancellationToken);
                        rcptSeries.CurrentValue++;
                        refundReceiptNo = NumberSeriesFormatter.Format(
                            rcptSeries.Prefix, rcptSeries.YearFormat, rcptSeries.Separator, rcptSeries.PadLength, rcptSeries.CurrentValue);
                        rcptSeries.UpdatedAt = billNow;
                        rcptSeries.UpdatedBy = "SYSTEM";

                        _context.BillingPayment.Add(new BillingPayment
                        {
                            PaymentId = Guid.NewGuid(),
                            HospitalId = appt.HospitalId,
                            PatientId = billEncounter.PatientId,
                            EncounterId = billEncounter.EncounterId,
                            ReceiptNo = refundReceiptNo,
                            PaymentType = BillingConstants.PaymentType.Refund,
                            PaymentMode = lastMode,
                            PaymentDescription = "Auto refund — appointment cancelled",
                            Amount = netCollected,
                            PaidAt = billNow,
                            CreatedAt = billNow,
                            CreatedBy = "SYSTEM",
                            UpdatedAt = billNow,
                            UpdatedBy = "SYSTEM"
                        });

                        // Drop the allocations so the cancelled invoice's paid total returns to zero.
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

                    // Void the charge lines.
                    var charges = await _context.BillingChargeEvent
                        .Where(c => c.EncounterId == billEncounter.EncounterId
                                 && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .ToListAsync(cancellationToken);
                    foreach (var c in charges)
                    {
                        c.StatusCode = BillingConstants.ChargeEventStatus.Void;
                        c.VoidedAt = billNow;
                        c.VoidedBy = "SYSTEM";
                        c.VoidReason = "Appointment cancelled";
                        c.UpdatedAt = billNow;
                        c.UpdatedBy = "SYSTEM";
                    }

                    // Cancel any invoice on this encounter (draft or finalized).
                    var invoices = await _context.BillingInvoice
                        .Where(i => i.EncounterId == billEncounter.EncounterId
                                 && i.StatusCode != BillingConstants.InvoiceStatus.Cancelled)
                        .ToListAsync(cancellationToken);
                    foreach (var inv in invoices)
                    {
                        inv.StatusCode = BillingConstants.InvoiceStatus.Cancelled;
                        inv.UpdatedAt = billNow;
                        inv.UpdatedBy = "SYSTEM";
                    }

                    billEncounter.StatusCode = BillingConstants.EncounterStatus.Cancelled;
                    billEncounter.UpdatedAt = billNow;
                    billEncounter.UpdatedBy = "SYSTEM";
                    billVoided = true;
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Send SMS to patient
                var patient = await _context.PatientRegistrations.FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);
                bool isSmsSent = false;
                if (patient != null && !string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment on {appt.ApptDate:yyyy-MM-dd} at {appt.StartAt:HH:mm} has been cancelled.";
                    isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                }

                var message = "Appointment cancelled successfully.";
                if (billRefunded)
                    message += $" ₹{refundAmount:0.##} was refunded (receipt {refundReceiptNo}) and the consultation bill was voided.";
                else if (billVoided)
                    message += " The unpaid consultation charge was voided.";

                return new CancelAppointmentResponseModel {
                    Success = true,
                    FinalStatus = AppConstants.AppointmentStatus_Cancelled,
                    IsReminderSent = isSmsSent,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                return new CancelAppointmentResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}