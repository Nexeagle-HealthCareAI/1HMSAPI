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
                var lastAppointment = await _context.Appointments
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

                var encounter = new Encounter
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

                // ── Auto-post OPD consultation fee ─────────────────────────────
                // When the billing policy's OPD consult trigger is AUTO and the visit is
                // chargeable (New / Old-with-fee, not Old/No-Fee), post the attending
                // doctor's OPD_CONSULT fee as a CONSULT charge line on this encounter.
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
                            _context.BillingChargeEvent.Add(new BillingChargeEvent
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
                            });
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
                        DoctorName = doctorName
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
