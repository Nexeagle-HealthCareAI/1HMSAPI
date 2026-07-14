using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetEarlyWarningScoreHistoryHandler : IRequestHandler<GetEarlyWarningScoreHistoryRequestModel, GetEarlyWarningScoreHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetEarlyWarningScoreHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetEarlyWarningScoreHistoryResponseModel> Handle(GetEarlyWarningScoreHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var scores = await _context.EarlyWarningScore
                .Where(s => s.HospitalId == request.HospitalId && s.AdmissionId == request.AdmissionId)
                .OrderByDescending(s => s.ScoredAt)
                .Select(s => new EarlyWarningScoreDataModel
                {
                    ScoreId = s.ScoreId,
                    TotalScore = s.TotalScore,
                    RiskBand = s.RiskBand,
                    RrScore = s.RrScore,
                    Spo2Score = s.Spo2Score,
                    O2Score = s.O2Score,
                    BpScore = s.BpScore,
                    PulseScore = s.PulseScore,
                    ConsciousnessScore = s.ConsciousnessScore,
                    TempScore = s.TempScore,
                    ScoredBy = s.ScoredBy,
                    ScoredAt = s.ScoredAt,
                    Notes = s.Notes,
                })
                .ToListAsync(cancellationToken);

            return new GetEarlyWarningScoreHistoryResponseModel { Scores = scores };
        }
    }
}
