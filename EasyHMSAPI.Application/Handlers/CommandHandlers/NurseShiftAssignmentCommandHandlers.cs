using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Nursing Station roster lifecycle: assign a nurse onto a ward+shift, and release her from it.
    // Deliberately does not check the assignee actually holds the "Nurse" role -- a ward sister or
    // AdminDoctor covering a shift can also be rostered here.
    public class NurseShiftAssignmentCommandHandlers :
        IRequestHandler<AssignNurseShiftRequestModel, AssignNurseShiftResponseModel>,
        IRequestHandler<ReleaseNurseShiftRequestModel, ReleaseNurseShiftResponseModel>
    {
        private readonly AppDbContext _context;

        public NurseShiftAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AssignNurseShiftResponseModel> Handle(AssignNurseShiftRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.NurseUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.WardCode))
                    return new AssignNurseShiftResponseModel { Success = false, Message = "HospitalId, NurseUserId and WardCode are required." };

                var shiftCode = request.ShiftCode?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(shiftCode) || !IpdConstants.ShiftCode.All.Contains(shiftCode))
                    return new AssignNurseShiftResponseModel { Success = false, Message = "Invalid shift code." };

                var wardCode = request.WardCode.Trim();
                var wardExists = await _context.BedMaster
                    .AnyAsync(b => b.HospitalId == request.HospitalId && b.WardCode == wardCode, cancellationToken);
                if (!wardExists)
                    return new AssignNurseShiftResponseModel { Success = false, Message = "Ward not found for this hospital." };

                var nurseInHospital = await _context.HospitalUsers
                    .AnyAsync(hu => hu.HospitalID == request.HospitalId && hu.UserID == request.NurseUserId, cancellationToken);
                if (!nurseInHospital)
                    return new AssignNurseShiftResponseModel { Success = false, Message = "This user does not belong to this hospital." };

                var shiftDate = request.ShiftDate?.Date;
                // Application-level pre-check for a friendly message on the common case;
                // UX_NSA_ActiveRoster (caught below as DbUpdateException) is the real concurrency backstop.
                var alreadyRostered = await _context.NurseShiftAssignment.AnyAsync(a =>
                    a.HospitalId == request.HospitalId &&
                    a.WardCode == wardCode &&
                    a.ShiftCode == shiftCode &&
                    a.ShiftDate == shiftDate &&
                    a.NurseUserId == request.NurseUserId &&
                    a.StatusCode == IpdConstants.NurseAssignmentStatus.Active, cancellationToken);
                if (alreadyRostered)
                    return new AssignNurseShiftResponseModel { Success = false, Message = "This nurse is already rostered to this ward for this shift." };

                var now = DateTime.UtcNow;
                var assignment = new NurseShiftAssignment
                {
                    NurseShiftAssignmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    NurseUserId = request.NurseUserId,
                    WardCode = wardCode,
                    ShiftCode = shiftCode,
                    ShiftDate = shiftDate,
                    StatusCode = IpdConstants.NurseAssignmentStatus.Active,
                    AssignedAt = now,
                    AssignedBy = request.LoggedInUserName,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };

                _context.NurseShiftAssignment.Add(assignment);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return new AssignNurseShiftResponseModel { Success = false, Message = "This nurse is already rostered to this ward for this shift." };
                }

                return new AssignNurseShiftResponseModel
                {
                    Success = true,
                    Message = "Nurse assigned.",
                    NurseShiftAssignmentId = assignment.NurseShiftAssignmentId,
                };
            }
            catch (Exception)
            {
                return new AssignNurseShiftResponseModel { Success = false, Message = "Error assigning the nurse." };
            }
        }

        public async Task<ReleaseNurseShiftResponseModel> Handle(ReleaseNurseShiftRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.NurseShiftAssignmentId == Guid.Empty)
                    return new ReleaseNurseShiftResponseModel { Success = false, Message = "HospitalId and NurseShiftAssignmentId are required." };

                var assignment = await _context.NurseShiftAssignment
                    .FirstOrDefaultAsync(a => a.NurseShiftAssignmentId == request.NurseShiftAssignmentId && a.HospitalId == request.HospitalId, cancellationToken);
                if (assignment == null)
                    return new ReleaseNurseShiftResponseModel { Success = false, Message = "Roster assignment not found." };

                if (assignment.StatusCode != IpdConstants.NurseAssignmentStatus.Active)
                    return new ReleaseNurseShiftResponseModel { Success = false, Message = "This assignment is already released." };

                var now = DateTime.UtcNow;
                assignment.StatusCode = IpdConstants.NurseAssignmentStatus.Released;
                assignment.UnassignedAt = now;
                assignment.UnassignedBy = request.LoggedInUserName;
                assignment.UpdatedAt = now;
                assignment.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ReleaseNurseShiftResponseModel { Success = true, Message = "Nurse released from shift." };
            }
            catch (Exception)
            {
                return new ReleaseNurseShiftResponseModel { Success = false, Message = "Error releasing the nurse." };
            }
        }
    }
}
