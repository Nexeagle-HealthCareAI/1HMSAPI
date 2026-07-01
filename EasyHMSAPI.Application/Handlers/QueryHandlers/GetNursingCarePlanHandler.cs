using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetNursingCarePlanHandler : IRequestHandler<GetNursingCarePlanRequestModel, GetNursingCarePlanResponseModel>
    {
        private readonly AppDbContext _context;

        public GetNursingCarePlanHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetNursingCarePlanResponseModel> Handle(GetNursingCarePlanRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetNursingCarePlanResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var items = await _context.NursingCarePlanItem
                    .Where(i => i.HospitalId == request.HospitalId && i.AdmissionId == request.AdmissionId)
                    .OrderBy(i => i.StatusCode == IpdConstants.NursingCarePlanStatus.Active ? 0 : 1)
                    .ThenByDescending(i => i.CreatedAt)
                    .Select(i => new NursingCarePlanItemModel
                    {
                        CarePlanItemId = i.CarePlanItemId,
                        NursingDiagnosis = i.NursingDiagnosis,
                        Goal = i.Goal,
                        PlannedInterventions = i.PlannedInterventions,
                        StatusCode = i.StatusCode,
                        CreatedAt = i.CreatedAt,
                        CreatedBy = i.CreatedBy,
                        ResolvedAt = i.ResolvedAt,
                        ResolvedBy = i.ResolvedBy,
                        ResolutionNotes = i.ResolutionNotes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetNursingCarePlanResponseModel { Success = true, Items = items };
            }
            catch (Exception)
            {
                return new GetNursingCarePlanResponseModel { Success = false, Message = "Error loading nursing care plan." };
            }
        }
    }
}
