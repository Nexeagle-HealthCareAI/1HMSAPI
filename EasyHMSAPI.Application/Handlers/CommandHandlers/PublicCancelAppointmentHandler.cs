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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Anonymous/bot-facing equivalent of CancelAppointmentHandler — deliberately a fully
    // independent implementation rather than a shared service (the staff handler is keyed on a
    // caller-supplied PatientId+HospitalId a public caller doesn't have; unifying them would mean
    // either handler reaching for fields the other's caller can't safely provide). Any future fix
    // to the token/billing/cache/SMS logic below needs to be made in both handlers.
    public class PublicCancelAppointmentHandler : IRequestHandler<PublicCancelAppointmentRequestModel, PublicCancelAppointmentResponseModel>
    {
        private const string CancelledByActor = "PUBLIC_API";

        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<PublicCancelAppointmentHandler> _logger;
        private readonly IMemoryCache _cache;

        public PublicCancelAppointmentHandler(AppDbContext context, ISmsService smsService, ILogger<PublicCancelAppointmentHandler> logger, IMemoryCache cache)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
            _cache = cache;
        }

        // Digits only, and Indian mobiles get their +91/0 trunk prefix stripped to a bare 10-digit
        // number — a WhatsApp number and a hospital-entered mobile are rarely formatted
        // identically, so a literal string match would reject legitimate callers.
        private static string NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
            return digits;
        }

        public async Task<PublicCancelAppointmentResponseModel> Handle(PublicCancelAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);

                if (appt == null)
                    return new PublicCancelAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                var patient = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);

                // Deliberately the same generic message as "not found" on a mobile mismatch —
                // don't tell an unauthenticated caller which half of the lookup failed.
                if (patient == null || NormalizeMobile(patient.Mobile) != NormalizeMobile(request.Mobile) || NormalizeMobile(request.Mobile) == string.Empty)
                    return new PublicCancelAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    return new PublicCancelAppointmentResponseModel { Success = false, Message = "This appointment is already cancelled." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    return new PublicCancelAppointmentResponseModel { Success = false, Message = "Cannot cancel a completed appointment." };

                bool billVoided = false;
                bool billRefunded = false;
                decimal refundAmount = 0m;
                string? refundReceiptNo = null;

                var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == appt.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                if (!doctorActive)
                {
                    return new PublicCancelAppointmentResponseModel { Success = false, Message = "Doctor is not active or has been revoked." };
                }

                appt.CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled;
                appt.LastStatusCodeAt = DateTime.UtcNow;
                appt.CancelledAt = DateTime.UtcNow;
                appt.CancelledBy = CancelledByActor;
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

                // ── Billing cleanup — same logic as CancelAppointmentHandler ──────────────
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

                    if (netCollected > 0)
                    {
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
                        c.VoidedAt = billNow;
                        c.VoidedBy = "SYSTEM";
                        c.VoidReason = "Appointment cancelled";
                        c.UpdatedAt = billNow;
                        c.UpdatedBy = "SYSTEM";
                    }

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

                _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(appt.HospitalId, appt.DoctorId, appt.ApptDate));

                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment on {appt.ApptDate:yyyy-MM-dd} at {appt.StartAt:HH:mm} has been cancelled.";
                    var isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                    _logger.LogInformation("Public-cancelled appointment {AppointmentId}, SMS sent: {IsSmsSent}", appt.ApptId, isSmsSent);
                }

                var message = "Appointment cancelled successfully.";
                if (billRefunded)
                    message += $" ₹{refundAmount:0.##} was refunded (receipt {refundReceiptNo}) and the consultation bill was voided.";
                else if (billVoided)
                    message += " The unpaid consultation charge was voided.";

                return new PublicCancelAppointmentResponseModel
                {
                    Success = true,
                    FinalStatus = AppConstants.AppointmentStatus_Cancelled,
                    Message = message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling appointment {AppointmentId} via public endpoint", request.AppointmentId);
                return new PublicCancelAppointmentResponseModel { Success = false, Message = "An error occurred while cancelling the appointment." };
            }
        }
    }
}
