using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Admissions for the hospital, newest first, with patient name and current bed (if assigned)
    /// folded in. This is the real-data list that GetBedBoardHandler can't provide on its own,
    /// since a fresh admission with no bed yet has no BedAssignment row to be found by.
    /// StatusFilter: ACTIVE (default — any non-terminal status) / DISCHARGED / ALL.
    /// </summary>
    public class GetActiveAdmissionsHandler : IRequestHandler<GetActiveAdmissionsRequestModel, GetActiveAdmissionsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetActiveAdmissionsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetActiveAdmissionsResponseModel> Handle(GetActiveAdmissionsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetActiveAdmissionsResponseModel { Success = false, Message = "HospitalId is required." };

                var statusFilter = string.IsNullOrWhiteSpace(request.StatusFilter) ? "ACTIVE" : request.StatusFilter.Trim().ToUpperInvariant();

                var query = _context.Admission.Where(a => a.HospitalId == request.HospitalId);
                query = statusFilter switch
                {
                    "DISCHARGED" => query.Where(a => a.StatusCode == IpdConstants.AdmissionStatus.Discharged),
                    "ALL" => query,
                    _ => query.Where(a => IpdConstants.AdmissionStatus.Active.Contains(a.StatusCode)),
                };

                IQueryable<Domain.Entities.Admission> admissionsQuery = query.OrderByDescending(a => a.AdmittedAt);
                if (statusFilter == "DISCHARGED" || statusFilter == "ALL")
                {
                    admissionsQuery = admissionsQuery.Take(200);
                }

                var admissions = await admissionsQuery.ToListAsync(cancellationToken);

                var patientIds = admissions.Select(a => a.PatientId).Distinct().ToList();
                var patientsById = await _context.PatientRegistrations
                    .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId!))
                    .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

                var admissionIds = admissions.Select(a => a.AdmissionId).ToList();
                var activeBeds = await _context.BedAssignment
                    .Where(b => b.HospitalId == request.HospitalId
                        && b.StatusCode == IpdConstants.BedAssignmentStatus.Active
                        && admissionIds.Contains(b.AdmissionId))
                    .ToListAsync(cancellationToken);
                var bedAssignmentByAdmission = activeBeds.ToDictionary(b => b.AdmissionId);

                var bedIds = activeBeds.Select(b => b.BedId).ToList();
                var bedsById = await _context.BedMaster
                    .Where(b => bedIds.Contains(b.BedId))
                    .ToDictionaryAsync(b => b.BedId, cancellationToken);

                // OrderByDescending + GroupBy-first (not ToDictionaryAsync) since a given admission
                // could in principle have more than one coverage row over time — same defensive
                // "latest wins" pattern StampIrdaiMilestoneHandler already uses for this table.
                var coverageRows = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && admissionIds.Contains(c.AdmissionId))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync(cancellationToken);
                var coverageByAdmission = coverageRows
                    .GroupBy(c => c.AdmissionId)
                    .ToDictionary(g => g.Key, g => g.First());

                // Admitting-consultant name — same Doctors→UserProfiles join GetHospitalDoctorsHandler uses.
                var doctorIds = admissions.Where(a => a.PrimaryDoctorId.HasValue).Select(a => a.PrimaryDoctorId!.Value).Distinct().ToList();
                var doctorUserIds = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Select(d => new { d.DoctorID, d.UserID })
                    .ToListAsync(cancellationToken);
                var userIds = doctorUserIds.Select(d => d.UserID).Distinct().ToList();
                var nameByUser = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameByUserLookup = nameByUser.GroupBy(n => n.UserID).ToDictionary(g => g.Key, g => g.First().FullName);
                var doctorNameById = doctorUserIds.ToDictionary(d => d.DoctorID, d => nameByUserLookup.TryGetValue(d.UserID, out var n) ? n : null);

                var items = admissions.Select(a =>
                {
                    patientsById.TryGetValue(a.PatientId, out var patient);

                    string? bedCode = null, wardName = null;
                    if (bedAssignmentByAdmission.TryGetValue(a.AdmissionId, out var assignment)
                        && bedsById.TryGetValue(assignment.BedId, out var bed))
                    {
                        bedCode = bed.BedCode;
                        wardName = bed.WardName;
                    }

                    coverageByAdmission.TryGetValue(a.AdmissionId, out var coverage);

                    return new ActiveAdmissionItem
                    {
                        AdmissionId = a.AdmissionId,
                        AdmissionNo = a.AdmissionNo,
                        AdmissionToken = a.AdmissionToken,
                        AdmissionType = a.AdmissionType,
                        StatusCode = a.StatusCode,
                        PayerType = a.PayerType,
                        AdmittedAt = a.AdmittedAt,
                        ExpectedDischargeAt = a.ExpectedDischargeAt,
                        AdmissionReason = a.AdmissionReason,
                        Diagnosis = a.Diagnosis,
                        DepositExpected = a.DepositExpected,
                        PrimaryDoctorId = a.PrimaryDoctorId,
                        PrimaryDoctorName = a.PrimaryDoctorId.HasValue && doctorNameById.TryGetValue(a.PrimaryDoctorId.Value, out var dn) ? dn : null,
                        ReferralSource = a.ReferralSource,
                        ReferralName = a.ReferralName,
                        ReferredByReferrerId = a.ReferredByReferrerId,
                        ReferringFacilityName = a.ReferringFacilityName,
                        ReferringFacilityType = a.ReferringFacilityType,
                        ReferringFacilityContact = a.ReferringFacilityContact,
                        PatientId = a.PatientId,
                        PatientName = patient?.FullName,
                        PatientAge = patient?.Age,
                        PatientSex = patient?.Sex,
                        PatientAddress = patient != null ? string.Join(", ", new[] { patient.AddressLine, patient.City, patient.State }.Where(x => !string.IsNullOrWhiteSpace(x))) : null,
                        Mobile = patient?.Mobile,
                        BedCode = bedCode,
                        WardName = wardName,
                        EncounterId = a.EncounterId,
                        PayerName = coverage?.PayerName,
                        PolicyOrBeneficiaryNo = coverage?.PolicyOrBeneficiaryNo,
                        PreAuthNo = coverage?.PreAuthNo,
                        PackageCode = coverage?.PackageCode,
                        SanctionedAmount = coverage?.SanctionedAmount,
                        EntitledRoomCategory = coverage?.EntitledRoomCategory,
                        OtPlanProcedureNameSnapshot = a.OtPlanProcedureNameSnapshot,
                        OtPlanSuggestedIcuLevel = a.OtPlanSuggestedIcuLevel,
                        PackageTypeNameSnapshot = a.PackageTypeNameSnapshot,
                    };
                }).ToList();

                return new GetActiveAdmissionsResponseModel { Success = true, Items = items };
            }
            catch (Exception)
            {
                return new GetActiveAdmissionsResponseModel { Success = false, Message = "Error loading active admissions." };
            }
        }
    }
}
