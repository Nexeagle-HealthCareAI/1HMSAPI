using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreateManualEncounterHandler : IRequestHandler<CreateManualEncounterRequestModel, CreateManualEncounterResponseModel>
    {
        private readonly AppDbContext _context;

        public CreateManualEncounterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateManualEncounterResponseModel> Handle(CreateManualEncounterRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PatientId) || request.HospitalId == Guid.Empty)
                    return new CreateManualEncounterResponseModel { Success = false, Message = "PatientId and HospitalId are required." };

                var patientExists = await _context.PatientRegistrations
                    .AnyAsync(p => p.PatientId == request.PatientId, cancellationToken);
                if (!patientExists)
                    return new CreateManualEncounterResponseModel { Success = false, Message = $"Patient {request.PatientId} not found." };

                var typeCode = string.IsNullOrWhiteSpace(request.EncounterType)
                    ? BillingConstants.EncounterType.Ipd
                    : request.EncounterType!.Trim().ToUpperInvariant();

                // Optional attending doctor name (for display on the bill).
                string? doctorName = null;
                if (request.DoctorId.HasValue)
                {
                    doctorName = await _context.Doctors
                        .Where(d => d.DoctorID == request.DoctorId.Value)
                        .Join(_context.UserProfiles, d => d.UserID, u => u.UserID, (d, u) => u.FullName)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var now = DateTime.UtcNow;
                var encounter = new Encounter
                {
                    EncounterId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterTypeCode = typeCode,
                    SourceType = "MANUAL",   // not tied to an appointment
                    SourceId = null,
                    PrimaryDoctorId = request.DoctorId,
                    StatusCode = BillingConstants.EncounterStatus.Open,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Encounter.Add(encounter);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateManualEncounterResponseModel
                {
                    Success = true,
                    Message = "Encounter created successfully.",
                    Data = new ManualEncounterData { EncounterId = encounter.EncounterId, DoctorName = doctorName }
                };
            }
            catch (Exception)
            {
                return new CreateManualEncounterResponseModel { Success = false, Message = "Error creating encounter." };
            }
        }
    }
}
