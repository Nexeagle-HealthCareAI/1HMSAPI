using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// WHO Surgical Safety Checklist — one row per SurgeryCase (get-or-create), 3 phases recorded
    /// in sequence: Sign-In before anaesthesia, Time-Out before incision, Sign-Out before leaving
    /// the theatre. Each phase requires the previous one completed first.
    /// </summary>
    public class SurgicalSafetyChecklistCommandHandlers :
        IRequestHandler<RecordSignInRequestModel, RecordSignInResponseModel>,
        IRequestHandler<RecordTimeOutRequestModel, RecordTimeOutResponseModel>,
        IRequestHandler<RecordSignOutRequestModel, RecordSignOutResponseModel>
    {
        private readonly AppDbContext _context;

        public SurgicalSafetyChecklistCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        private async Task<SurgicalSafetyChecklist> GetOrCreateAsync(Guid hospitalId, Guid surgeryCaseId, CancellationToken cancellationToken)
        {
            var checklist = await _context.SurgicalSafetyChecklist
                .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCaseId && c.HospitalId == hospitalId, cancellationToken);
            if (checklist != null)
                return checklist;

            var now = DateTime.UtcNow;
            checklist = new SurgicalSafetyChecklist
            {
                ChecklistId = Guid.NewGuid(),
                HospitalId = hospitalId,
                SurgeryCaseId = surgeryCaseId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _context.SurgicalSafetyChecklist.Add(checklist);
            return checklist;
        }

        public async Task<RecordSignInResponseModel> Handle(RecordSignInRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new RecordSignInResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var surgeryCaseExists = await _context.SurgeryCase
                    .AnyAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (!surgeryCaseExists)
                    return new RecordSignInResponseModel { Success = false, Message = "Surgery case not found." };

                var checklist = await GetOrCreateAsync(request.HospitalId, request.SurgeryCaseId, cancellationToken);
                var now = DateTime.UtcNow;
                checklist.SignInItemsJson = JsonSerializer.Serialize(request.Items);
                checklist.SignInNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                checklist.SignInCompletedAt = now;
                checklist.SignInCompletedBy = request.LoggedInUserName;
                checklist.UpdatedAt = now;
                checklist.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new RecordSignInResponseModel { Success = true, Message = "Sign-In recorded." };
            }
            catch (Exception)
            {
                return new RecordSignInResponseModel { Success = false, Message = "Error recording Sign-In." };
            }
        }

        public async Task<RecordTimeOutResponseModel> Handle(RecordTimeOutRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new RecordTimeOutResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var checklist = await _context.SurgicalSafetyChecklist
                    .FirstOrDefaultAsync(c => c.SurgeryCaseId == request.SurgeryCaseId && c.HospitalId == request.HospitalId, cancellationToken);
                if (checklist == null || checklist.SignInCompletedAt == null)
                    return new RecordTimeOutResponseModel { Success = false, Message = "Sign-In must be completed before Time-Out." };

                var now = DateTime.UtcNow;
                checklist.TimeOutItemsJson = JsonSerializer.Serialize(request.Items);
                checklist.TimeOutNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                checklist.TimeOutCompletedAt = now;
                checklist.TimeOutCompletedBy = request.LoggedInUserName;
                checklist.UpdatedAt = now;
                checklist.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new RecordTimeOutResponseModel { Success = true, Message = "Time-Out recorded." };
            }
            catch (Exception)
            {
                return new RecordTimeOutResponseModel { Success = false, Message = "Error recording Time-Out." };
            }
        }

        public async Task<RecordSignOutResponseModel> Handle(RecordSignOutRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new RecordSignOutResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var checklist = await _context.SurgicalSafetyChecklist
                    .FirstOrDefaultAsync(c => c.SurgeryCaseId == request.SurgeryCaseId && c.HospitalId == request.HospitalId, cancellationToken);
                if (checklist == null || checklist.TimeOutCompletedAt == null)
                    return new RecordSignOutResponseModel { Success = false, Message = "Time-Out must be completed before Sign-Out." };

                var now = DateTime.UtcNow;
                checklist.SignOutItemsJson = JsonSerializer.Serialize(request.Items);
                checklist.SignOutNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                checklist.SignOutCompletedAt = now;
                checklist.SignOutCompletedBy = request.LoggedInUserName;
                checklist.UpdatedAt = now;
                checklist.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new RecordSignOutResponseModel { Success = true, Message = "Sign-Out recorded." };
            }
            catch (Exception)
            {
                return new RecordSignOutResponseModel { Success = false, Message = "Error recording Sign-Out." };
            }
        }
    }
}
