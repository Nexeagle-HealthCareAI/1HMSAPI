using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetApacheIIScoreHistoryHandler : IRequestHandler<GetApacheIIScoreHistoryRequestModel, GetApacheIIScoreHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetApacheIIScoreHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetApacheIIScoreHistoryResponseModel> Handle(GetApacheIIScoreHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var scores = await _context.ApacheIIScore
                .Where(s => s.HospitalId == request.HospitalId && s.AdmissionId == request.AdmissionId)
                .OrderByDescending(s => s.ScoredAt)
                .Select(s => new ApacheIIScoreDataModel
                {
                    ApacheIIScoreId = s.ApacheIIScoreId,
                    TotalScore = s.TotalScore,
                    ChronicHealthCategory = s.ChronicHealthCategory,
                    ScoredBy = s.ScoredBy,
                    ScoredAt = s.ScoredAt,
                    Notes = s.Notes,
                })
                .ToListAsync(cancellationToken);

            return new GetApacheIIScoreHistoryResponseModel { Scores = scores };
        }
    }
}
