using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RapidResponseCommandHandlers :
        IRequestHandler<ActivateRapidResponseRequestModel, ActivateRapidResponseResponseModel>,
        IRequestHandler<MarkRapidResponseArrivedRequestModel, UpdateRapidResponseResponseModel>,
        IRequestHandler<ResolveRapidResponseRequestModel, UpdateRapidResponseResponseModel>
    {
        private readonly AppDbContext _context;

        public RapidResponseCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ActivateRapidResponseResponseModel> Handle(ActivateRapidResponseRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new ActivateRapidResponseResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var triggerReason = request.TriggerReason?.Trim().ToUpperInvariant() ?? string.Empty;
                if (!IpdConstants.RrtTriggerReason.All.Contains(triggerReason))
                    return new ActivateRapidResponseResponseModel { Success = false, Message = "Invalid trigger reason." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new ActivateRapidResponseResponseModel { Success = false, Message = "Admission not found." };

                var now = DateTime.UtcNow;
                var activation = new RapidResponseActivation
                {
                    ActivationId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    TriggerReason = triggerReason,
                    TriggeredEwsScore = request.TriggeredEwsScore,
                    CalledBy = request.LoggedInUserName ?? "Unknown",
                    CalledAt = now,
                    RespondingTeam = string.IsNullOrWhiteSpace(request.RespondingTeam) ? null : request.RespondingTeam.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.RapidResponseActivation.Add(activation);
                await _context.SaveChangesAsync(cancellationToken);

                return new ActivateRapidResponseResponseModel { Success = true, Message = "Rapid Response activated.", ActivationId = activation.ActivationId };
            }
            catch (Exception)
            {
                return new ActivateRapidResponseResponseModel { Success = false, Message = "Error activating Rapid Response." };
            }
        }

        public async Task<UpdateRapidResponseResponseModel> Handle(MarkRapidResponseArrivedRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ActivationId == Guid.Empty)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "HospitalId and ActivationId are required." };

                var activation = await _context.RapidResponseActivation
                    .FirstOrDefaultAsync(r => r.ActivationId == request.ActivationId && r.HospitalId == request.HospitalId, cancellationToken);
                if (activation == null)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "Activation not found." };
                if (activation.ResolvedAt != null)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "This activation is already resolved." };

                activation.ArrivedAt = DateTime.UtcNow;
                activation.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                return new UpdateRapidResponseResponseModel { Success = true, Message = "Marked as arrived." };
            }
            catch (Exception)
            {
                return new UpdateRapidResponseResponseModel { Success = false, Message = "Error recording arrival." };
            }
        }

        public async Task<UpdateRapidResponseResponseModel> Handle(ResolveRapidResponseRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ActivationId == Guid.Empty)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "HospitalId and ActivationId are required." };

                var outcome = request.Outcome?.Trim().ToUpperInvariant() ?? string.Empty;
                if (!IpdConstants.RrtOutcome.All.Contains(outcome))
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "Invalid outcome." };

                var activation = await _context.RapidResponseActivation
                    .FirstOrDefaultAsync(r => r.ActivationId == request.ActivationId && r.HospitalId == request.HospitalId, cancellationToken);
                if (activation == null)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "Activation not found." };
                if (activation.ResolvedAt != null)
                    return new UpdateRapidResponseResponseModel { Success = false, Message = "This activation is already resolved." };

                var now = DateTime.UtcNow;
                activation.Outcome = outcome;
                activation.OutcomeNotes = string.IsNullOrWhiteSpace(request.OutcomeNotes) ? null : request.OutcomeNotes.Trim();
                activation.ResolvedAt = now;
                activation.UpdatedAt = now;
                activation.UpdatedBy = request.LoggedInUserName;
                await _context.SaveChangesAsync(cancellationToken);

                return new UpdateRapidResponseResponseModel { Success = true, Message = "Rapid Response resolved." };
            }
            catch (Exception)
            {
                return new UpdateRapidResponseResponseModel { Success = false, Message = "Error resolving Rapid Response." };
            }
        }
    }
}
