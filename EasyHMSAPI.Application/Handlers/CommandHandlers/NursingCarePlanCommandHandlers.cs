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
    /// Nursing care plan — free-text nursing diagnosis/goal/interventions (not a coded NANDA-I
    /// taxonomy), with an ACTIVE/RESOLVED/DISCONTINUED lifecycle mirroring
    /// ClinicalOrderCommandHandlers' Discontinue shape.
    /// </summary>
    public class NursingCarePlanCommandHandlers :
        IRequestHandler<CreateNursingCarePlanItemRequestModel, CreateNursingCarePlanItemResponseModel>,
        IRequestHandler<ResolveNursingCarePlanItemRequestModel, ResolveNursingCarePlanItemResponseModel>
    {
        private readonly AppDbContext _context;

        public NursingCarePlanCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateNursingCarePlanItemResponseModel> Handle(CreateNursingCarePlanItemRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new CreateNursingCarePlanItemResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (string.IsNullOrWhiteSpace(request.NursingDiagnosis))
                    return new CreateNursingCarePlanItemResponseModel { Success = false, Message = "Nursing diagnosis is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new CreateNursingCarePlanItemResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new CreateNursingCarePlanItemResponseModel { Success = false, Message = "Admission is not active." };

                var now = DateTime.UtcNow;
                var item = new NursingCarePlanItem
                {
                    CarePlanItemId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    NursingDiagnosis = request.NursingDiagnosis.Trim(),
                    Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim(),
                    PlannedInterventions = string.IsNullOrWhiteSpace(request.PlannedInterventions) ? null : request.PlannedInterventions.Trim(),
                    StatusCode = IpdConstants.NursingCarePlanStatus.Active,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    CreatedByUserId = request.LoggedInUserId,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.NursingCarePlanItem.Add(item);

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateNursingCarePlanItemResponseModel { Success = true, Message = "Care plan item added.", CarePlanItemId = item.CarePlanItemId };
            }
            catch (Exception)
            {
                return new CreateNursingCarePlanItemResponseModel { Success = false, Message = "Error adding care plan item." };
            }
        }

        public async Task<ResolveNursingCarePlanItemResponseModel> Handle(ResolveNursingCarePlanItemRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.CarePlanItemId == Guid.Empty)
                    return new ResolveNursingCarePlanItemResponseModel { Success = false, Message = "HospitalId and CarePlanItemId are required." };

                var statusCode = request.StatusCode?.Trim().ToUpperInvariant();
                if (statusCode != IpdConstants.NursingCarePlanStatus.Resolved && statusCode != IpdConstants.NursingCarePlanStatus.Discontinued)
                    return new ResolveNursingCarePlanItemResponseModel { Success = false, Message = "StatusCode must be RESOLVED or DISCONTINUED." };

                var item = await _context.NursingCarePlanItem
                    .FirstOrDefaultAsync(i => i.CarePlanItemId == request.CarePlanItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (item == null)
                    return new ResolveNursingCarePlanItemResponseModel { Success = false, Message = "Care plan item not found." };
                if (item.StatusCode != IpdConstants.NursingCarePlanStatus.Active)
                    return new ResolveNursingCarePlanItemResponseModel { Success = false, Message = "This item is already resolved or discontinued." };

                var now = DateTime.UtcNow;
                item.StatusCode = statusCode;
                item.ResolvedAt = now;
                item.ResolvedBy = request.LoggedInUserName;
                item.ResolvedByUserId = request.LoggedInUserId;
                item.ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes) ? null : request.ResolutionNotes.Trim();
                item.UpdatedAt = now;
                item.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ResolveNursingCarePlanItemResponseModel { Success = true, Message = "Care plan item updated.", CarePlanItemId = item.CarePlanItemId, StatusCode = item.StatusCode };
            }
            catch (Exception)
            {
                return new ResolveNursingCarePlanItemResponseModel { Success = false, Message = "Error updating care plan item." };
            }
        }
    }
}
