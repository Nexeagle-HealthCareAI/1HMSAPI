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
