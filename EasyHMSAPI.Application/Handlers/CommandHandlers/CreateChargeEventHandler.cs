using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreateChargeEventHandler : IRequestHandler<CreateChargeEventRequestModel, CreateChargeEventResponseModel>
    {
        private readonly AppDbContext _context;

        public CreateChargeEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateChargeEventResponseModel> Handle(CreateChargeEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Bill against the specific appointment when provided; otherwise the latest.
                var lastAppointment = request.AppointmentId.HasValue
                    ? await _context.Appointments
                        .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId.Value, cancellationToken)
                    : await _context.Appointments
                        .Where(a => a.PatientId == request.PatientId)
                        .OrderByDescending(a => a.ApptDate)
                        .FirstOrDefaultAsync(cancellationToken);

                if (lastAppointment == null)
                {
                    return new CreateChargeEventResponseModel
                    {
                        Success = false,
                        Message = $"No appointment found for patient {request.PatientId}"
                    };
                }

                var doctorName = await _context.Doctors
                    .Where(d => d.DoctorID == lastAppointment.DoctorId)
                    .Join(_context.UserProfiles,
                          d => d.UserID,
                          u => u.UserID,
                          (d, u) => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken);

                var encounterTypeCode = string.IsNullOrWhiteSpace(request.EncounterType)
                    ? BillingConstants.EncounterType.Opd
                    : request.EncounterType!.Trim().ToUpperInvariant();

                // ── Idempotency ────────────────────────────────────────────────
                // Reuse the encounter already created for this appointment (e.g. the
                // booking popup and the dashboard both firing) instead of making a second
                // one, and never post a duplicate consult charge.
                var encounter = await _context.Encounter
                    .FirstOrDefaultAsync(e => e.HospitalId == request.HospitalId
                                           && e.SourceType == "Appointments"
                                           && e.SourceId == lastAppointment.ApptId
                                           && e.EncounterTypeCode == encounterTypeCode,
                                           cancellationToken);

                if (encounter != null)
                {
                    var existingConsult = await _context.BillingChargeEvent
                        .Where(c => c.EncounterId == encounter.EncounterId && c.CategoryCode == "CONSULT")
                        .Select(c => new { c.ChargeEventId, c.NetAmount })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingConsult != null)
                    {
                        // Consultation already charged for this appointment — return idempotently.
                        return new CreateChargeEventResponseModel
                        {
                            Success = true,
                            Message = "Consultation already charged for this appointment.",
                            Data = new ChargeEventData
                            {
                                EncounterId = encounter.EncounterId,
                                DoctorName = doctorName,
                                ConsultChargePosted = false,
                                ConsultAlreadyCharged = true,
                                ConsultFee = existingConsult.NetAmount,
                                ConsultChargeEventId = existingConsult.ChargeEventId
                            }
                        };
                    }
                }
                else
                {
                    encounter = new Encounter
                    {
                        EncounterId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterTypeCode = encounterTypeCode,
                        SourceType = "Appointments",
                        SourceId = lastAppointment.ApptId,
                        PrimaryDoctorId = lastAppointment.DoctorId,
                        StatusCode = BillingConstants.EncounterStatus.Open,
                        // Carry the referrer from the appointment so incentive accrual can attribute it.
                        ReferredByReferrerId = lastAppointment.ReferredByReferrerId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = request.LoggedInUserName
                    };
                    _context.Encounter.Add(encounter);
                }

                // ── Auto-post OPD consultation fee ─────────────────────────────
                // When the billing policy's OPD consult trigger is AUTO and the visit is
                // chargeable (New / Old-with-fee, not Old/No-Fee), post the attending
                // doctor's OPD_CONSULT fee as a CONSULT charge line on this encounter.
                bool consultChargePosted = false;
                decimal consultFee = 0m;
                Guid? consultChargeEventId = null;
                if (encounterTypeCode == BillingConstants.EncounterType.Opd)
                {
                    var policy = await _context.BillingPolicy
                        .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                    var triggerOn = string.Equals(policy?.OpdConsultTrigger, "AUTO", StringComparison.OrdinalIgnoreCase);
                    var chargeable = !string.Equals(lastAppointment.AppointmentType, AppConstants.AppointmentType_OldNoFee, StringComparison.OrdinalIgnoreCase);

                    if (triggerOn && chargeable)
                    {
                        var fee = await _context.DoctorFees
                            .Where(f => f.HospitalId == request.HospitalId
                                     && f.DoctorId == lastAppointment.DoctorId
                                     && f.FeeType == "OPD_CONSULT"
                                     && f.IsActive)
                            .Select(f => (decimal?)f.Amount)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (fee.HasValue && fee.Value > 0)
                        {
                            var at = DateTime.UtcNow;
                            var chargeEvent = new BillingChargeEvent
                            {
                                ChargeEventId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                PatientId = request.PatientId,
                                EncounterId = encounter.EncounterId,
                                SourceModule = BillingConstants.SourceModule.Opd,
                                CategoryCode = "CONSULT",
                                DisplayName = $"Consultation — {doctorName ?? "Doctor"}",
                                Qty = 1,
                                UnitPrice = fee.Value,
                                DiscountAmount = 0,
                                NetAmount = fee.Value,
                                IsTaxInclusive = false,
                                IsInterState = false,
                                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                                ServiceDate = at,
                                PostedAt = at,
                                PostedBy = request.LoggedInUserName,
                                CreatedAt = at,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = at,
                                UpdatedBy = request.LoggedInUserName
                            };
                            _context.BillingChargeEvent.Add(chargeEvent);
                            consultChargePosted = true;
                            consultFee = fee.Value;
                            consultChargeEventId = chargeEvent.ChargeEventId;
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateChargeEventResponseModel
                {
                    Success = true,
                    Message = "Encounter created successfully.",
                    Data = new ChargeEventData
                    {
                        EncounterId = encounter.EncounterId,
                        DoctorName = doctorName,
                        ConsultChargePosted = consultChargePosted,
                        ConsultFee = consultFee,
                        ConsultAlreadyCharged = false,
                        ConsultChargeEventId = consultChargeEventId
                    }
                };
            }
            catch (Exception)
            {
                return new CreateChargeEventResponseModel
                {
                    Success = false,
                    Message = "Error creating encounter."
                };
            }
        }
    }
}
