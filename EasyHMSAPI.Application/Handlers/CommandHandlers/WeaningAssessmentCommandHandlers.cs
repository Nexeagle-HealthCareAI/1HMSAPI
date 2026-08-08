using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class WeaningAssessmentCommandHandlers : IRequestHandler<RecordWeaningAssessmentRequestModel, RecordWeaningAssessmentResponseModel>
    {
        private readonly AppDbContext _context;

        public WeaningAssessmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordWeaningAssessmentResponseModel> Handle(RecordWeaningAssessmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordWeaningAssessmentResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordWeaningAssessmentResponseModel { Success = false, Message = "Admission not found." };

                var now = DateTime.UtcNow;
                var assessment = new WeaningAssessment
                {
                    WeaningAssessmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    SatPerformed = request.SatPerformed,
                    // Can't have "passed" a trial that was never performed.
                    SatPassed = request.SatPerformed && request.SatPassed,
                    SbtPerformed = request.SbtPerformed,
                    SbtPassed = request.SbtPerformed && request.SbtPassed,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    AssessedBy = request.LoggedInUserName ?? "Unknown",
                    AssessedAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.WeaningAssessment.Add(assessment);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordWeaningAssessmentResponseModel { Success = true, Message = "Weaning assessment recorded.", WeaningAssessmentId = assessment.WeaningAssessmentId };
            }
            catch (Exception)
            {
                return new RecordWeaningAssessmentResponseModel { Success = false, Message = "Error recording weaning assessment." };
            }
        }
    }
}
