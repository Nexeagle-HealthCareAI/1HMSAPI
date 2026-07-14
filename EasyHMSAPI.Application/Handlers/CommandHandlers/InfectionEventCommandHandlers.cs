using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Insert-only log of a diagnosed device-associated infection (CLABSI/CAUTI/VAP) or
    /// other HAI. Feeds GetInfectionRateSummaryHandler's infections-per-1000-device-days view.
    /// </summary>
    public class InfectionEventCommandHandlers : IRequestHandler<LogInfectionEventRequestModel, LogInfectionEventResponseModel>
    {
        private readonly AppDbContext _context;

        public InfectionEventCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LogInfectionEventResponseModel> Handle(LogInfectionEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new LogInfectionEventResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (string.IsNullOrWhiteSpace(request.InfectionType) || !IpdConstants.InfectionType.All.Contains(request.InfectionType))
                    return new LogInfectionEventResponseModel { Success = false, Message = "A valid infection type is required." };
                if (string.IsNullOrWhiteSpace(request.DiagnosedByDoctorName))
                    return new LogInfectionEventResponseModel { Success = false, Message = "Diagnosing doctor name is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new LogInfectionEventResponseModel { Success = false, Message = "Admission not found." };

                if (request.DeviceAssignmentId.HasValue)
                {
                    var deviceBelongs = await _context.DeviceAssignment
                        .AnyAsync(d => d.DeviceAssignmentId == request.DeviceAssignmentId.Value && d.AdmissionId == admission.AdmissionId, cancellationToken);
                    if (!deviceBelongs)
                        return new LogInfectionEventResponseModel { Success = false, Message = "The related device does not belong to this admission." };
                }

                var now = DateTime.UtcNow;
                var infectionEvent = new InfectionEvent
                {
                    InfectionEventId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    DeviceAssignmentId = request.DeviceAssignmentId,
                    InfectionType = request.InfectionType,
                    DiagnosedAt = request.DiagnosedAt ?? now,
                    DiagnosedByDoctorName = request.DiagnosedByDoctorName.Trim(),
                    CultureOrganism = string.IsNullOrWhiteSpace(request.CultureOrganism) ? null : request.CultureOrganism.Trim(),
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.InfectionEvent.Add(infectionEvent);
                await _context.SaveChangesAsync(cancellationToken);

                return new LogInfectionEventResponseModel { Success = true, Message = "Infection event logged.", InfectionEventId = infectionEvent.InfectionEventId };
            }
            catch (Exception)
            {
                return new LogInfectionEventResponseModel { Success = false, Message = "Error logging infection event." };
            }
        }
    }
}
