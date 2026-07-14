using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// "Referred by" reassignment (Overview tab "Change referrer"). Same transactional shape as
    /// AdmissionDoctorAssignmentCommandHandlers.Handle(ChangeAdmittingDoctorRequestModel): release
    /// the current ACTIVE AdmissionReferrerAssignment and insert a new one atomically, via
    /// AdmissionReferrerAssignmentHelper (also shared by
    /// AdmissionStatusCommandHandlers.Handle(UpdateAdmissionDetailsRequestModel) so both entry points
    /// produce one consistent history trail).
    /// </summary>
    public class AdmissionReferrerAssignmentCommandHandlers :
        IRequestHandler<ChangeAdmissionReferrerRequestModel, ChangeAdmissionReferrerResponseModel>
    {
        private readonly AppDbContext _context;

        public AdmissionReferrerAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChangeAdmissionReferrerResponseModel> Handle(ChangeAdmissionReferrerRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReferralSource))
                    return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "HospitalId, AdmissionId and ReferralSource are required." };

                var normalizedSource = request.ReferralSource.Trim().ToUpperInvariant();
                if (!IpdConstants.ReferralSourceType.All.Contains(normalizedSource))
                    return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Invalid referral source." };
                if ((normalizedSource == "DOCTOR" || normalizedSource == "OTHER") && (!request.ReferrerId.HasValue || request.ReferrerId == Guid.Empty))
                    return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "A referrer must be selected for this referral source." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var admission = await _context.Admission
                            .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                        if (admission == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Admission not found." };
                        }
                        if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Admission is closed — its referrer can no longer be changed." };
                        }

                        var referrerId = normalizedSource == "SELF" ? null : request.ReferrerId;
                        var referrerName = normalizedSource == "SELF" ? null : request.ReferrerName;
                        var referrerType = normalizedSource == "SELF" ? null : request.ReferrerType;

                        if (referrerId.HasValue)
                        {
                            var referrerExists = await _context.Referrers
                                .AnyAsync(r => r.HospitalId == request.HospitalId && r.ReferrerId == referrerId.Value, cancellationToken);
                            if (!referrerExists)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Referrer not found for this hospital." };
                            }
                        }

                        var now = DateTime.UtcNow;
                        var newAssignment = await AdmissionReferrerAssignmentHelper.ChangeReferrerAsync(
                            _context, admission, normalizedSource, referrerId, referrerName, referrerType, request.LoggedInUserName, now, cancellationToken);
                        if (newAssignment == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "This is already the current referrer." };
                        }
                        if (!string.IsNullOrWhiteSpace(request.Notes))
                            newAssignment.Notes = request.Notes.Trim();
                        admission.UpdatedAt = now;
                        admission.UpdatedBy = request.LoggedInUserName;

                        try
                        {
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateException)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Another referrer change is already in progress for this admission." };
                        }

                        await tx.CommitAsync(cancellationToken);

                        return new ChangeAdmissionReferrerResponseModel
                        {
                            Success = true,
                            Message = "Referrer changed.",
                            AssignmentId = newAssignment.AssignmentId,
                            AssignedAt = now,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Error changing the referrer." };
                    }
                });
            }
            catch (Exception)
            {
                return new ChangeAdmissionReferrerResponseModel { Success = false, Message = "Error changing the referrer." };
            }
        }
    }
}
