using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Returns the admission linked to a billing encounter (active one preferred, else most recent).
    /// Success with null Data means the encounter has not been admitted.
    /// </summary>
    public class GetAdmissionByEncounterHandler : IRequestHandler<GetAdmissionByEncounterRequestModel, GetAdmissionByEncounterResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionByEncounterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionByEncounterResponseModel> Handle(GetAdmissionByEncounterRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                    return new GetAdmissionByEncounterResponseModel { Success = false, Message = "HospitalId and EncounterId are required." };

                var admission = await _context.Admission
                    .Where(a => a.EncounterId == request.EncounterId && a.HospitalId == request.HospitalId)
                    .OrderByDescending(a => a.StatusCode == "ADMITTED")
                    .ThenByDescending(a => a.AdmittedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (admission == null)
                    return new GetAdmissionByEncounterResponseModel { Success = true, Data = null };

                return new GetAdmissionByEncounterResponseModel
                {
                    Success = true,
                    Data = new AdmissionInfo
                    {
                        AdmissionId = admission.AdmissionId,
                        AdmissionNo = admission.AdmissionNo,
                        PatientId = admission.PatientId,
                        EncounterId = admission.EncounterId,
                        AdmittedAt = admission.AdmittedAt,
                        DischargedAt = admission.DischargedAt,
                        StatusCode = admission.StatusCode,
                        AdmissionReason = admission.AdmissionReason,
                    }
                };
            }
            catch (Exception)
            {
                return new GetAdmissionByEncounterResponseModel { Success = false, Message = "Error loading admission." };
            }
        }
    }
}
