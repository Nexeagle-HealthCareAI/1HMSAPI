using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Adds an existing doctor to another hospital in the caller's chain: creates the per-hospital
    /// rows (HospitalUser membership, DoctorDepartment, optional fee, section prefs) while reusing
    /// the doctor's single Doctor row. Guards: caller owns the target's chain; the user is a
    /// Doctor/AdminDoctor; not already a member. No new schema — all tables are already per-hospital.
    /// </summary>
    public class AddDoctorToHospitalHandler : IRequestHandler<AddDoctorToHospitalRequestModel, AddDoctorToHospitalResponseModel>
    {
        private static readonly string[] DoctorRoleNames = { "Doctor", "AdminDoctor" };
        private readonly AppDbContext _context;

        public AddDoctorToHospitalHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AddDoctorToHospitalResponseModel> Handle(AddDoctorToHospitalRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.DoctorId == Guid.Empty || request.TargetHospitalId == Guid.Empty || request.DepartmentId == Guid.Empty)
                    return Fail("Doctor, target hospital and department are required.");

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (doctor == null) return Fail("Doctor not found.");

                // Caller must own the chain the target hospital belongs to.
                var target = await _context.Hospitals.FirstOrDefaultAsync(h => h.HospitalID == request.TargetHospitalId, cancellationToken);
                if (target == null) return Fail("Target hospital not found.");
                if (target.ChainId == null)
                    return Fail("The target hospital is not part of a chain.");
                var ownsChain = await _context.HospitalChains
                    .AnyAsync(c => c.ChainId == target.ChainId && c.OwnerUserId == request.CallerUserId, cancellationToken);
                if (!ownsChain)
                    return Fail("You can only add doctors to hospitals in a chain you own.");

                // Only Doctor / AdminDoctor users may work at multiple hospitals.
                var isDoctorRole = await (
                    from ur in _context.UserRoles
                    join r in _context.Roles on ur.RoleID equals r.RoleID
                    where ur.UserID == doctor.UserID && DoctorRoleNames.Contains(r.RoleName)
                    select ur.UserID).AnyAsync(cancellationToken);
                if (!isDoctorRole)
                    return Fail("Only a Doctor or AdminDoctor can work at multiple hospitals.");

                // Already a member? Make it a friendly no-op.
                var alreadyMember = await _context.HospitalUsers
                    .AnyAsync(hu => hu.HospitalID == request.TargetHospitalId && hu.UserID == doctor.UserID, cancellationToken);
                if (alreadyMember)
                    return new AddDoctorToHospitalResponseModel { Success = true, AlreadyMember = true, Message = "This doctor already works at the selected hospital." };

                var now = DateTime.UtcNow;
                var employeeId = await _context.UserProfiles
                    .Where(up => up.UserID == doctor.UserID)
                    .Select(up => up.EmployeeID)
                    .FirstOrDefaultAsync(cancellationToken);

                // 1) Hospital membership (drives validation, hospitals/mine + the switcher).
                _context.HospitalUsers.Add(new HospitalUser
                {
                    HospitalUserID = Guid.NewGuid(),
                    HospitalID = request.TargetHospitalId,
                    UserID = doctor.UserID,
                    EmployeeID = employeeId ?? string.Empty,
                    IsPrimary = false,
                    CreatedAt = now,
                });

                // 2) Department assignment for the target hospital (drives the booking doctor list).
                var hasDept = await _context.DoctorDepartments.AnyAsync(
                    dd => dd.DoctorID == doctor.DoctorID && dd.DepartmentID == request.DepartmentId && dd.HospitalId == request.TargetHospitalId, cancellationToken);
                if (!hasDept)
                {
                    _context.DoctorDepartments.Add(new DoctorDepartment
                    {
                        DoctorDepartmentID = Guid.NewGuid(),
                        DoctorID = doctor.DoctorID,
                        DepartmentID = request.DepartmentId,
                        AssignedAt = now,
                        HospitalId = request.TargetHospitalId,
                    });
                }

                // Ensure the department is mapped to the target hospital.
                var hasMapping = await _context.HospitalDepartmentMappings.AnyAsync(
                    m => m.HospitalID == request.TargetHospitalId && m.DepartmentID == request.DepartmentId, cancellationToken);
                if (!hasMapping)
                {
                    _context.HospitalDepartmentMappings.Add(new HospitalDepartmentMapping
                    {
                        MappingID = Guid.NewGuid(),
                        HospitalID = request.TargetHospitalId,
                        DepartmentID = request.DepartmentId,
                        IsActive = true,
                        MappedAt = now,
                    });
                }

                // 2b) Carry the doctor's specializations to the target hospital (listing/display parity).
                var doctorSpecIds = await _context.DoctorSpecializations
                    .Where(s => s.DoctorID == doctor.DoctorID)
                    .Select(s => s.SpecializationID)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                if (doctorSpecIds.Count > 0)
                {
                    var atTarget = await _context.DoctorSpecializations
                        .Where(s => s.DoctorID == doctor.DoctorID && s.HospitalId == request.TargetHospitalId)
                        .Select(s => s.SpecializationID)
                        .ToListAsync(cancellationToken);
                    foreach (var specId in doctorSpecIds.Except(atTarget))
                    {
                        _context.DoctorSpecializations.Add(new DoctorSpecialization
                        {
                            DoctorSpecializationID = Guid.NewGuid(),
                            DoctorID = doctor.DoctorID,
                            SpecializationID = specId,
                            AssignedAt = now,
                            HospitalId = request.TargetHospitalId,
                        });
                    }
                }

                // 3) Optional per-hospital OPD consult fee.
                if (request.ConsultFee.HasValue && request.ConsultFee.Value > 0)
                {
                    var hasFee = await _context.DoctorFees.AnyAsync(
                        f => f.HospitalId == request.TargetHospitalId && f.DoctorId == doctor.DoctorID && f.FeeType == "OPD_CONSULT" && f.IsActive, cancellationToken);
                    if (!hasFee)
                    {
                        _context.DoctorFees.Add(new DoctorFee
                        {
                            DoctorFeeId = Guid.NewGuid(),
                            HospitalId = request.TargetHospitalId,
                            DoctorId = doctor.DoctorID,
                            FeeType = "OPD_CONSULT",
                            Amount = request.ConsultFee.Value,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                            UpdatedAt = now,
                            UpdatedBy = request.LoggedInUserName,
                        });
                    }
                }

                // 4) Default e-prescription section preferences for the target hospital.
                var hasPref = await _context.DoctorSectionPreferences.AnyAsync(
                    p => p.HospitalId == request.TargetHospitalId && p.DoctorId == doctor.DoctorID, cancellationToken);
                if (!hasPref)
                {
                    _context.DoctorSectionPreferences.Add(new DoctorSectionPreference
                    {
                        PreferenceId = Guid.NewGuid(),
                        HospitalId = request.TargetHospitalId,
                        DoctorId = doctor.DoctorID,
                        Vitals = true, ChiefComplaint = true, History = true, Comorbidity = true,
                        Examination = true, Diagnosis = true, Investigations = true, Procedures = true,
                        Medications = true, PrivateNotes = true, CertificatesAndNotes = true, Immunizations = true,
                        FollowUpAndReferral = true, NonPharmacologicalAdvice = true, Attachments = true,
                        CreatedAtUtc = now, UpdatedAtUtc = now,
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
                return new AddDoctorToHospitalResponseModel { Success = true, Message = "Doctor added to the hospital." };
            }
            catch (Exception)
            {
                return Fail("Error adding the doctor to the hospital.");
            }
        }

        private static AddDoctorToHospitalResponseModel Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
