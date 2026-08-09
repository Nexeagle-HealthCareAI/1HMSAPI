using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetWeaningAssessmentHistoryHandler : IRequestHandler<GetWeaningAssessmentHistoryRequestModel, GetWeaningAssessmentHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetWeaningAssessmentHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetWeaningAssessmentHistoryResponseModel> Handle(GetWeaningAssessmentHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var rows = await _context.WeaningAssessment.AsNoTracking()
                .Where(w => w.HospitalId == request.HospitalId && w.AdmissionId == request.AdmissionId)
                .OrderByDescending(w => w.AssessedAt)
                .ToListAsync(cancellationToken);

            return new GetWeaningAssessmentHistoryResponseModel
            {
                Assessments = rows.Select(w => new WeaningAssessmentDataModel
                {
                    WeaningAssessmentId = w.WeaningAssessmentId,
                    SatPerformed = w.SatPerformed,
                    SatPassed = w.SatPassed,
                    SbtPerformed = w.SbtPerformed,
                    SbtPassed = w.SbtPassed,
                    AssessedBy = w.AssessedBy,
                    AssessedAt = w.AssessedAt,
                    Notes = w.Notes,
                }).ToList(),
            };
        }
    }
}
