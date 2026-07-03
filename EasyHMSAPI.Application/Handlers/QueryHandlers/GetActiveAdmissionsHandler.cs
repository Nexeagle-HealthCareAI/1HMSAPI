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

                var admissions = await query
                    .OrderByDescending(a => a.AdmittedAt)
                    .ToListAsync(cancellationToken);

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

                    return new ActiveAdmissionItem
                    {
                        AdmissionId = a.AdmissionId,
                        AdmissionNo = a.AdmissionNo,
                        AdmissionType = a.AdmissionType,
                        StatusCode = a.StatusCode,
                        PayerType = a.PayerType,
                        AdmittedAt = a.AdmittedAt,
                        AdmissionReason = a.AdmissionReason,
                        Diagnosis = a.Diagnosis,
                        PatientId = a.PatientId,
                        PatientName = patient?.FullName,
                        PatientAge = patient?.Age,
                        PatientSex = patient?.Sex,
                        BedCode = bedCode,
                        WardName = wardName,
                        EncounterId = a.EncounterId,
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
