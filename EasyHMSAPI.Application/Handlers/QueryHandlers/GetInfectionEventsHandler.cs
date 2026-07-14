using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetInfectionEventsHandler : IRequestHandler<GetInfectionEventsRequestModel, GetInfectionEventsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetInfectionEventsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetInfectionEventsResponseModel> Handle(GetInfectionEventsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetInfectionEventsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var events = await _context.InfectionEvent
                    .Where(e => e.HospitalId == request.HospitalId && e.AdmissionId == request.AdmissionId)
                    .OrderByDescending(e => e.DiagnosedAt)
                    .Select(e => new InfectionEventItem
                    {
                        InfectionEventId = e.InfectionEventId,
                        DeviceAssignmentId = e.DeviceAssignmentId,
                        InfectionType = e.InfectionType,
                        DiagnosedAt = e.DiagnosedAt,
                        DiagnosedByDoctorName = e.DiagnosedByDoctorName,
                        CultureOrganism = e.CultureOrganism,
                        Notes = e.Notes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetInfectionEventsResponseModel { Success = true, Events = events };
            }
            catch (Exception)
            {
                return new GetInfectionEventsResponseModel { Success = false, Message = "Error loading infection events." };
            }
        }
    }
}
