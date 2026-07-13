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
    /// Admitting-doctor reassignment (Overview tab "Change doctor"). Same transactional shape as
    /// BedAssignmentCommandHandlers.TransferBed: release the current ACTIVE AdmissionDoctorAssignment
    /// and insert a new one atomically, via AdmissionDoctorAssignmentHelper (also shared by
    /// AdmissionStatusCommandHandlers.Handle(UpdateAdmissionDetailsRequestModel) so both entry points
    /// produce one consistent history trail).
    /// </summary>
    public class AdmissionDoctorAssignmentCommandHandlers :
        IRequestHandler<ChangeAdmittingDoctorRequestModel, ChangeAdmittingDoctorResponseModel>
    {
        private readonly AppDbContext _context;

        public AdmissionDoctorAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChangeAdmittingDoctorResponseModel> Handle(ChangeAdmittingDoctorRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.DoctorId == Guid.Empty)
                    return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "HospitalId, AdmissionId and DoctorId are required." };

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
                            return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Admission not found." };
                        }
                        if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Admission is closed — its doctor can no longer be changed." };
                        }
                        if (admission.PrimaryDoctorId == request.DoctorId)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "This doctor is already the admitting doctor." };
                        }

                        var doctorExists = await _context.DoctorDepartments
                            .AnyAsync(dd => dd.HospitalId == request.HospitalId && dd.DoctorID == request.DoctorId, cancellationToken);
                        if (!doctorExists)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Doctor not found for this hospital." };
                        }

                        var now = DateTime.UtcNow;
                        var newAssignment = await AdmissionDoctorAssignmentHelper.ChangeDoctorAsync(
                            _context, admission, request.DoctorId, request.LoggedInUserName, now, cancellationToken);
                        if (newAssignment != null && !string.IsNullOrWhiteSpace(request.Notes))
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
                            return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Another doctor change is already in progress for this admission." };
                        }

                        await tx.CommitAsync(cancellationToken);

                        return new ChangeAdmittingDoctorResponseModel
                        {
                            Success = true,
                            Message = "Doctor changed.",
                            AssignmentId = newAssignment?.AssignmentId,
                            DoctorId = request.DoctorId,
                            AssignedAt = now,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Error changing the admitting doctor." };
                    }
                });
            }
            catch (Exception)
            {
                return new ChangeAdmittingDoctorResponseModel { Success = false, Message = "Error changing the admitting doctor." };
            }
        }
    }
}
