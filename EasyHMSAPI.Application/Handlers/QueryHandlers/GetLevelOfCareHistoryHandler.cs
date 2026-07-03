using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetLevelOfCareHistoryHandler : IRequestHandler<GetLevelOfCareHistoryRequestModel, GetLevelOfCareHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetLevelOfCareHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetLevelOfCareHistoryResponseModel> Handle(GetLevelOfCareHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var history = await _context.IcuLevelOfCare
                .Where(l => l.HospitalId == request.HospitalId && l.AdmissionId == request.AdmissionId)
                .OrderByDescending(l => l.AssessedAt)
                .Select(l => new IcuLevelOfCareDataModel
                {
                    IcuLevelOfCareId = l.IcuLevelOfCareId,
                    Level = l.Level,
                    Reason = l.Reason,
                    Notes = l.Notes,
                    AssessedBy = l.AssessedBy,
                    AssessedAt = l.AssessedAt,
                })
                .ToListAsync(cancellationToken);

            return new GetLevelOfCareHistoryResponseModel { History = history };
        }
    }
}
