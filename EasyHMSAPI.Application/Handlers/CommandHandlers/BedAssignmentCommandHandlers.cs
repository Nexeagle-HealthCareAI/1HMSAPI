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
    /// Bed-board mutations: Assign (fresh bed for an admission with none), Release (free the bed,
    /// e.g. on discharge), Transfer (move an admission to a different bed). The DB's filtered unique
    /// index (one ACTIVE BedAssignment per bed) is the concurrency backstop — a race is caught as a
    /// DbUpdateException and turned into a friendly "already occupied" message.
    /// </summary>
    public class BedAssignmentCommandHandlers :
        IRequestHandler<AssignBedRequestModel, AssignBedResponseModel>,
        IRequestHandler<ReleaseBedRequestModel, ReleaseBedResponseModel>,
        IRequestHandler<TransferBedRequestModel, TransferBedResponseModel>
    {
        private readonly AppDbContext _context;

        public BedAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AssignBedResponseModel> Handle(AssignBedRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.BedId == Guid.Empty)
                    return new AssignBedResponseModel { Success = false, Message = "HospitalId, AdmissionId and BedId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new AssignBedResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new AssignBedResponseModel { Success = false, Message = "Admission is not active." };

                var alreadyAssigned = await _context.BedAssignment
                    .AnyAsync(a => a.AdmissionId == request.AdmissionId && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
                if (alreadyAssigned)
                    return new AssignBedResponseModel { Success = false, Message = "Admission already has a bed assigned. Use transfer instead." };

                var bed = await _context.BedMaster
                    .FirstOrDefaultAsync(b => b.BedId == request.BedId && b.HospitalId == request.HospitalId, cancellationToken);
                if (bed == null)
                    return new AssignBedResponseModel { Success = false, Message = "Bed not found." };

                var now = DateTime.UtcNow;
                var assignment = new BedAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = request.AdmissionId,
                    BedId = bed.BedId,
                    AssignedAt = now,
                    AssignedBy = request.LoggedInUserName,
                    DailyRateSnapshot = bed.BedDailyRateOverride ?? bed.WardRoomDailyRate,
                    StatusCode = IpdConstants.BedAssignmentStatus.Active,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.BedAssignment.Add(assignment);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return new AssignBedResponseModel { Success = false, Message = "That bed is already occupied by another patient." };
                }

                return new AssignBedResponseModel
                {
                    Success = true,
                    Message = "Bed assigned.",
                    BedAssignmentId = assignment.AssignmentId,
                    BedId = assignment.BedId,
                    AssignedAt = assignment.AssignedAt,
                };
            }
            catch (Exception)
            {
                return new AssignBedResponseModel { Success = false, Message = "Error assigning bed." };
            }
        }

        public async Task<ReleaseBedResponseModel> Handle(ReleaseBedRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new ReleaseBedResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var assignment = await _context.BedAssignment
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId
                        && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
                if (assignment == null)
                    return new ReleaseBedResponseModel { Success = false, Message = "No active bed assignment found for this admission." };

                var now = DateTime.UtcNow;
                assignment.StatusCode = IpdConstants.BedAssignmentStatus.Released;
                assignment.ReleasedAt = now;
                assignment.ReleasedBy = request.LoggedInUserName;
                if (!string.IsNullOrWhiteSpace(request.Notes)) assignment.Notes = request.Notes.Trim();
                assignment.UpdatedAt = now;
                assignment.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ReleaseBedResponseModel
                {
                    Success = true,
                    Message = "Bed released.",
                    BedAssignmentId = assignment.AssignmentId,
                    ReleasedAt = assignment.ReleasedAt,
                };
            }
            catch (Exception)
            {
                return new ReleaseBedResponseModel { Success = false, Message = "Error releasing bed." };
            }
        }

        public async Task<TransferBedResponseModel> Handle(TransferBedRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty || request.NewBedId == Guid.Empty)
                    return new TransferBedResponseModel { Success = false, Message = "HospitalId, AdmissionId and NewBedId are required." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var current = await _context.BedAssignment
                            .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId
                                && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);
                        if (current == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new TransferBedResponseModel { Success = false, Message = "Admission has no active bed to transfer from. Use assign instead." };
                        }
                        if (current.BedId == request.NewBedId)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new TransferBedResponseModel { Success = false, Message = "Admission is already assigned to that bed." };
                        }

                        var newBed = await _context.BedMaster
                            .FirstOrDefaultAsync(b => b.BedId == request.NewBedId && b.HospitalId == request.HospitalId, cancellationToken);
                        if (newBed == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new TransferBedResponseModel { Success = false, Message = "Bed not found." };
                        }

                        var now = DateTime.UtcNow;
                        current.StatusCode = IpdConstants.BedAssignmentStatus.Released;
                        current.ReleasedAt = now;
                        current.ReleasedBy = request.LoggedInUserName;
                        if (!string.IsNullOrWhiteSpace(request.Notes)) current.Notes = request.Notes.Trim();
                        current.UpdatedAt = now;
                        current.UpdatedBy = request.LoggedInUserName;

                        var newAssignment = new BedAssignment
                        {
                            AssignmentId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            AdmissionId = request.AdmissionId,
                            BedId = newBed.BedId,
                            AssignedAt = now,
                            AssignedBy = request.LoggedInUserName,
                            DailyRateSnapshot = newBed.BedDailyRateOverride ?? newBed.WardRoomDailyRate,
                            StatusCode = IpdConstants.BedAssignmentStatus.Active,
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                            UpdatedAt = now,
                            UpdatedBy = request.LoggedInUserName,
                        };
                        _context.BedAssignment.Add(newAssignment);

                        try
                        {
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateException)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new TransferBedResponseModel { Success = false, Message = "That bed is already occupied by another patient." };
                        }

                        await tx.CommitAsync(cancellationToken);

                        return new TransferBedResponseModel
                        {
                            Success = true,
                            Message = "Bed transferred.",
                            PreviousBedAssignmentId = current.AssignmentId,
                            NewBedAssignmentId = newAssignment.AssignmentId,
                            NewBedId = newAssignment.BedId,
                            TransferredAt = now,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new TransferBedResponseModel { Success = false, Message = "Error transferring bed." };
                    }
                });
            }
            catch (Exception)
            {
                return new TransferBedResponseModel { Success = false, Message = "Error transferring bed." };
            }
        }
    }
}
