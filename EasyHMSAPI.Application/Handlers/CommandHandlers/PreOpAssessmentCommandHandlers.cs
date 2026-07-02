using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class PreOpAssessmentCommandHandlers : IRequestHandler<RecordPreOpAssessmentRequestModel, RecordPreOpAssessmentResponseModel>
    {
        private readonly AppDbContext _context;

        public PreOpAssessmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordPreOpAssessmentResponseModel> Handle(RecordPreOpAssessmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new RecordPreOpAssessmentResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var asaGrade = request.AsaGrade?.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(asaGrade) && !IpdConstants.AsaGrade.All.Contains(asaGrade))
                    return new RecordPreOpAssessmentResponseModel { Success = false, Message = "Invalid ASA grade." };

                var surgeryCase = await _context.SurgeryCase
                    .FirstOrDefaultAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (surgeryCase == null)
                    return new RecordPreOpAssessmentResponseModel { Success = false, Message = "Surgery case not found." };

                var now = DateTime.UtcNow;
                var assessment = new PreOpAssessment
                {
                    PreOpAssessmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    SurgeryCaseId = surgeryCase.SurgeryCaseId,
                    AsaGrade = string.IsNullOrWhiteSpace(asaGrade) ? null : asaGrade,
                    NpoConfirmed = request.NpoConfirmed,
                    AllergiesReviewed = request.AllergiesReviewed,
                    InvestigationsReviewed = request.InvestigationsReviewed,
                    ConsentConfirmed = request.ConsentConfirmed,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    AssessedBy = request.LoggedInUserName ?? "Unknown",
                    AssessedAt = now,
                };
                _context.PreOpAssessment.Add(assessment);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordPreOpAssessmentResponseModel { Success = true, Message = "Assessment recorded.", PreOpAssessmentId = assessment.PreOpAssessmentId };
            }
            catch (Exception)
            {
                return new RecordPreOpAssessmentResponseModel { Success = false, Message = "Error recording pre-op assessment." };
            }
        }
    }
}
