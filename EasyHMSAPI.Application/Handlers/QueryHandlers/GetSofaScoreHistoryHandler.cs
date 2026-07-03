using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetSofaScoreHistoryHandler : IRequestHandler<GetSofaScoreHistoryRequestModel, GetSofaScoreHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetSofaScoreHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetSofaScoreHistoryResponseModel> Handle(GetSofaScoreHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var scores = await _context.SofaScore
                .Where(s => s.HospitalId == request.HospitalId && s.AdmissionId == request.AdmissionId)
                .OrderByDescending(s => s.ScoredAt)
                .Select(s => new SofaScoreDataModel
                {
                    SofaScoreId = s.SofaScoreId,
                    TotalScore = s.TotalScore,
                    RespiratoryScore = s.RespiratoryScore,
                    CoagulationScore = s.CoagulationScore,
                    LiverScore = s.LiverScore,
                    CardiovascularScore = s.CardiovascularScore,
                    CnsScore = s.CnsScore,
                    RenalScore = s.RenalScore,
                    ScoredBy = s.ScoredBy,
                    ScoredAt = s.ScoredAt,
                    Notes = s.Notes,
                })
                .ToListAsync(cancellationToken);

            return new GetSofaScoreHistoryResponseModel { Scores = scores };
        }
    }
}
