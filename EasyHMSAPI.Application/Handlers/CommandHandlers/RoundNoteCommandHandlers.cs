using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Creates a SOAP round note. Pure insert — edits become addendum rows (never overwrite
    /// history); this handler accepts/validates ParentNoteId+AddendumReason when supplied.
    /// </summary>
    public class RoundNoteCommandHandlers : IRequestHandler<CreateRoundNoteRequestModel, CreateRoundNoteResponseModel>
    {
        private readonly AppDbContext _context;

        public RoundNoteCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateRoundNoteResponseModel> Handle(CreateRoundNoteRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new CreateRoundNoteResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var hasContent = !string.IsNullOrWhiteSpace(request.Subjective) || !string.IsNullOrWhiteSpace(request.Objective)
                    || !string.IsNullOrWhiteSpace(request.Assessment) || !string.IsNullOrWhiteSpace(request.Plan)
                    || !string.IsNullOrWhiteSpace(request.Diagnosis);
                if (!hasContent)
                    return new CreateRoundNoteResponseModel { Success = false, Message = "At least one note section is required." };

                var isAddendum = request.ParentNoteId.HasValue;
                if (isAddendum && string.IsNullOrWhiteSpace(request.AddendumReason))
                    return new CreateRoundNoteResponseModel { Success = false, Message = "An addendum reason is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new CreateRoundNoteResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new CreateRoundNoteResponseModel { Success = false, Message = "Admission is not active." };

                if (isAddendum)
                {
                    var parentExists = await _context.RoundNote.AnyAsync(r => r.RoundNoteId == request.ParentNoteId!.Value && r.AdmissionId == admission.AdmissionId, cancellationToken);
                    if (!parentExists)
                        return new CreateRoundNoteResponseModel { Success = false, Message = "Parent note not found." };
                }

                var now = DateTime.UtcNow;
                var note = new RoundNote
                {
                    RoundNoteId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    DoctorId = request.DoctorId,
                    DoctorName = string.IsNullOrWhiteSpace(request.DoctorName) ? request.LoggedInUserName : request.DoctorName.Trim(),
                    NotedAt = request.NotedAt ?? now,
                    Subjective = string.IsNullOrWhiteSpace(request.Subjective) ? null : request.Subjective.Trim(),
                    Objective = string.IsNullOrWhiteSpace(request.Objective) ? null : request.Objective.Trim(),
                    Assessment = string.IsNullOrWhiteSpace(request.Assessment) ? null : request.Assessment.Trim(),
                    Plan = string.IsNullOrWhiteSpace(request.Plan) ? null : request.Plan.Trim(),
                    Diagnosis = string.IsNullOrWhiteSpace(request.Diagnosis) ? null : request.Diagnosis.Trim(),
                    IsAddendum = isAddendum,
                    ParentNoteId = request.ParentNoteId,
                    AddendumReason = isAddendum ? request.AddendumReason!.Trim() : null,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.RoundNote.Add(note);

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateRoundNoteResponseModel
                {
                    Success = true,
                    Message = isAddendum ? "Addendum added." : "Round note recorded.",
                    RoundNoteId = note.RoundNoteId,
                    IsAddendum = isAddendum,
                    ParentNoteId = note.ParentNoteId,
                };
            }
            catch (Exception)
            {
                return new CreateRoundNoteResponseModel { Success = false, Message = "Error recording round note." };
            }
        }
    }
}
