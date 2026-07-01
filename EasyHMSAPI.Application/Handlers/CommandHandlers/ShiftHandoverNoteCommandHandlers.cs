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
    /// SBAR shift handover. Enforces the free-text-fallback contract server-side (mirrors
    /// CK_SHN_FreeTextOrSbar with a friendlier message): free-text mode nulls out every SBAR
    /// field regardless of client input; structured mode requires only Situation. Insert-only —
    /// Acknowledge is a narrow bolt-on that only touches 3 ack columns, never the content.
    /// </summary>
    public class ShiftHandoverNoteCommandHandlers :
        IRequestHandler<CreateShiftHandoverNoteRequestModel, CreateShiftHandoverNoteResponseModel>,
        IRequestHandler<AcknowledgeShiftHandoverRequestModel, AcknowledgeShiftHandoverResponseModel>
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        private readonly AppDbContext _context;

        public ShiftHandoverNoteCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateShiftHandoverNoteResponseModel> Handle(CreateShiftHandoverNoteRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var shiftCode = request.ShiftCode?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(shiftCode) || !IpdConstants.ShiftCode.All.Contains(shiftCode))
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Invalid shift code." };

                if (string.IsNullOrWhiteSpace(request.OutgoingNurseName))
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Outgoing nurse name is required." };

                if (request.IsFreeText && string.IsNullOrWhiteSpace(request.FreeTextNote))
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Free-text note is required." };
                if (!request.IsFreeText && string.IsNullOrWhiteSpace(request.Situation))
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Situation is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Admission is not active." };

                var now = DateTime.UtcNow;
                var shiftDate = (now + IstOffset).Date;

                var note = new ShiftHandoverNote
                {
                    ShiftHandoverNoteId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    ShiftCode = shiftCode,
                    ShiftDate = shiftDate,
                    OutgoingNurseName = request.OutgoingNurseName.Trim(),
                    OutgoingNurseUserId = request.OutgoingNurseUserId,
                    IncomingNurseName = string.IsNullOrWhiteSpace(request.IncomingNurseName) ? null : request.IncomingNurseName.Trim(),
                    IncomingNurseUserId = request.IncomingNurseUserId,
                    IncomingAckAt = null,
                    IsFreeText = request.IsFreeText,
                    FreeTextNote = request.IsFreeText ? request.FreeTextNote!.Trim() : null,
                    Situation = request.IsFreeText ? null : (string.IsNullOrWhiteSpace(request.Situation) ? null : request.Situation.Trim()),
                    Background = request.IsFreeText ? null : (string.IsNullOrWhiteSpace(request.Background) ? null : request.Background.Trim()),
                    Assessment = request.IsFreeText ? null : (string.IsNullOrWhiteSpace(request.Assessment) ? null : request.Assessment.Trim()),
                    Recommendation = request.IsFreeText ? null : (string.IsNullOrWhiteSpace(request.Recommendation) ? null : request.Recommendation.Trim()),
                    HandoverAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.ShiftHandoverNote.Add(note);

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateShiftHandoverNoteResponseModel { Success = true, Message = "Handover recorded.", ShiftHandoverNoteId = note.ShiftHandoverNoteId };
            }
            catch (Exception)
            {
                return new CreateShiftHandoverNoteResponseModel { Success = false, Message = "Error recording handover." };
            }
        }

        public async Task<AcknowledgeShiftHandoverResponseModel> Handle(AcknowledgeShiftHandoverRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ShiftHandoverNoteId == Guid.Empty)
                    return new AcknowledgeShiftHandoverResponseModel { Success = false, Message = "HospitalId and ShiftHandoverNoteId are required." };
                if (string.IsNullOrWhiteSpace(request.IncomingNurseName))
                    return new AcknowledgeShiftHandoverResponseModel { Success = false, Message = "Incoming nurse name is required." };

                var note = await _context.ShiftHandoverNote
                    .FirstOrDefaultAsync(s => s.ShiftHandoverNoteId == request.ShiftHandoverNoteId && s.HospitalId == request.HospitalId, cancellationToken);
                if (note == null)
                    return new AcknowledgeShiftHandoverResponseModel { Success = false, Message = "Handover note not found." };

                note.IncomingNurseName = request.IncomingNurseName.Trim();
                note.IncomingNurseUserId = request.LoggedInUserId;
                note.IncomingAckAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new AcknowledgeShiftHandoverResponseModel { Success = true, Message = "Handover acknowledged." };
            }
            catch (Exception)
            {
                return new AcknowledgeShiftHandoverResponseModel { Success = false, Message = "Error acknowledging handover." };
            }
        }
    }
}
