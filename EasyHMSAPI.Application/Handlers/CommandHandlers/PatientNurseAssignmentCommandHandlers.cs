using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Per-patient nurse assignment lifecycle: assign a nurse to a specific patient for a shift, and
    // release her from it. Deliberately independent of the ward roster (NurseShiftAssignment) --
    // the nurse does not need to already be rostered to the patient's ward, matching the ward
    // roster's own "doesn't check the Nurse role" posture of not over-constraining who can be
    // assigned.
    public class PatientNurseAssignmentCommandHandlers :
        IRequestHandler<AssignPatientNurseRequestModel, AssignPatientNurseResponseModel>,
        IRequestHandler<ReleasePatientNurseRequestModel, ReleasePatientNurseResponseModel>
    {
        private readonly AppDbContext _context;

        public PatientNurseAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AssignPatientNurseResponseModel> Handle(AssignPatientNurseRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.NurseUserId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new AssignPatientNurseResponseModel { Success = false, Message = "HospitalId, NurseUserId and AdmissionId are required." };

                var shiftCode = request.ShiftCode?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(shiftCode) || !IpdConstants.ShiftCode.All.Contains(shiftCode))
                    return new AssignPatientNurseResponseModel { Success = false, Message = "Invalid shift code." };

                var admissionExists = await _context.Admission
                    .AnyAsync(a => a.HospitalId == request.HospitalId && a.AdmissionId == request.AdmissionId, cancellationToken);
                if (!admissionExists)
                    return new AssignPatientNurseResponseModel { Success = false, Message = "Admission not found for this hospital." };

                var nurseInHospital = await _context.HospitalUsers
                    .AnyAsync(hu => hu.HospitalID == request.HospitalId && hu.UserID == request.NurseUserId, cancellationToken);
                if (!nurseInHospital)
                    return new AssignPatientNurseResponseModel { Success = false, Message = "This user does not belong to this hospital." };

                var shiftDate = request.ShiftDate?.Date;
                // Application-level pre-check for a friendly message on the common case;
                // UX_PNA_ActiveAssignment (caught below as DbUpdateException) is the real concurrency backstop.
                var alreadyAssigned = await _context.PatientNurseAssignment.AnyAsync(a =>
                    a.HospitalId == request.HospitalId &&
                    a.AdmissionId == request.AdmissionId &&
                    a.ShiftCode == shiftCode &&
                    a.ShiftDate == shiftDate &&
                    a.NurseUserId == request.NurseUserId &&
                    a.StatusCode == IpdConstants.NurseAssignmentStatus.Active, cancellationToken);
                if (alreadyAssigned)
                    return new AssignPatientNurseResponseModel { Success = false, Message = "This nurse is already assigned to this patient for this shift." };

                var now = DateTime.UtcNow;
                var assignment = new PatientNurseAssignment
                {
                    PatientNurseAssignmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    NurseUserId = request.NurseUserId,
                    AdmissionId = request.AdmissionId,
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

                _context.PatientNurseAssignment.Add(assignment);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return new AssignPatientNurseResponseModel { Success = false, Message = "This nurse is already assigned to this patient for this shift." };
                }

                return new AssignPatientNurseResponseModel
                {
                    Success = true,
                    Message = "Nurse assigned.",
                    PatientNurseAssignmentId = assignment.PatientNurseAssignmentId,
                };
            }
            catch (Exception)
            {
                return new AssignPatientNurseResponseModel { Success = false, Message = "Error assigning the nurse." };
            }
        }

        public async Task<ReleasePatientNurseResponseModel> Handle(ReleasePatientNurseRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.PatientNurseAssignmentId == Guid.Empty)
                    return new ReleasePatientNurseResponseModel { Success = false, Message = "HospitalId and PatientNurseAssignmentId are required." };

                var assignment = await _context.PatientNurseAssignment
                    .FirstOrDefaultAsync(a => a.PatientNurseAssignmentId == request.PatientNurseAssignmentId && a.HospitalId == request.HospitalId, cancellationToken);
                if (assignment == null)
                    return new ReleasePatientNurseResponseModel { Success = false, Message = "Assignment not found." };

                if (assignment.StatusCode != IpdConstants.NurseAssignmentStatus.Active)
                    return new ReleasePatientNurseResponseModel { Success = false, Message = "This assignment is already released." };

                var now = DateTime.UtcNow;
                assignment.StatusCode = IpdConstants.NurseAssignmentStatus.Released;
                assignment.UnassignedAt = now;
                assignment.UnassignedBy = request.LoggedInUserName;
                assignment.UpdatedAt = now;
                assignment.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ReleasePatientNurseResponseModel { Success = true, Message = "Nurse released from patient." };
            }
            catch (Exception)
            {
                return new ReleasePatientNurseResponseModel { Success = false, Message = "Error releasing the nurse." };
            }
        }
    }
}
