using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Live bed board: every bed for the hospital (optionally filtered to one ward), left-joined
    /// against its ACTIVE assignment (if any) plus that admission's patient — so every bed shows,
    /// occupied or not. Ward grouping is a client/server concern on top of this flat list; there's
    /// no separate Ward entity, WardCode/WardName live on BedMaster.
    /// </summary>
    public class GetBedBoardHandler : IRequestHandler<GetBedBoardRequestModel, GetBedBoardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBedBoardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBedBoardResponseModel> Handle(GetBedBoardRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetBedBoardResponseModel { Success = false, Message = "HospitalId is required." };

                var bedsQuery = _context.BedMaster.Where(b => b.HospitalId == request.HospitalId);
                if (!string.IsNullOrWhiteSpace(request.WardCode))
                    bedsQuery = bedsQuery.Where(b => b.WardCode == request.WardCode);

                var beds = await bedsQuery
                    .OrderBy(b => b.WardCode).ThenBy(b => b.SortOrder)
                    .ToListAsync(cancellationToken);

                var bedIds = beds.Select(b => b.BedId).ToList();
                var activeAssignments = await _context.BedAssignment
                    .Where(a => a.HospitalId == request.HospitalId
                        && a.StatusCode == IpdConstants.BedAssignmentStatus.Active
                        && bedIds.Contains(a.BedId))
                    .ToListAsync(cancellationToken);

                var admissionIds = activeAssignments.Select(a => a.AdmissionId).ToList();
                var admissionsById = await _context.Admission
                    .Where(a => admissionIds.Contains(a.AdmissionId))
                    .ToDictionaryAsync(a => a.AdmissionId, cancellationToken);

                var patientIds = admissionsById.Values.Select(a => a.PatientId).Distinct().ToList();
                var patientsById = await _context.PatientRegistrations
                    .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId!))
                    .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

                var assignmentsByBed = activeAssignments.ToDictionary(a => a.BedId);

                var items = beds.Select(b =>
                {
                    var item = new BedBoardItem
                    {
                        BedId = b.BedId,
                        WardCode = b.WardCode,
                        WardName = b.WardName,
                        WardType = b.WardType,
                        FloorNo = b.FloorNo,
                        RoomCode = b.RoomCode,
                        RoomType = b.RoomType,
                        BedCode = b.BedCode,
                        BedName = b.BedName,
                        StatusCode = b.StatusCode,
                        GenderRestriction = b.GenderRestriction,
                        IsActive = b.IsActive,
                        EffectiveDailyRate = b.BedDailyRateOverride ?? b.WardRoomDailyRate,
                        SortOrder = b.SortOrder,
                    };

                    if (assignmentsByBed.TryGetValue(b.BedId, out var assignment)
                        && admissionsById.TryGetValue(assignment.AdmissionId, out var admission))
                    {
                        item.BedAssignmentId = assignment.AssignmentId;
                        item.AdmissionId = admission.AdmissionId;
                        item.AdmissionNo = admission.AdmissionNo;
                        item.AdmissionType = admission.AdmissionType;
                        item.PayerType = admission.PayerType;
                        item.AssignedAt = assignment.AssignedAt;
                        item.PatientId = admission.PatientId;

                        if (patientsById.TryGetValue(admission.PatientId, out var patient))
                        {
                            item.PatientName = patient.FullName;
                            item.PatientAge = patient.Age;
                            item.PatientSex = patient.Sex;
                        }
                    }

                    return item;
                }).ToList();

                return new GetBedBoardResponseModel { Success = true, Items = items };
            }
            catch (Exception)
            {
                return new GetBedBoardResponseModel { Success = false, Message = "Error loading bed board." };
            }
        }
    }
}
