using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDoctorFeesHandler : IRequestHandler<GetDoctorFeesRequestModel, GetDoctorFeesResponseModel>
    {
        private const string OpdConsult = "OPD_CONSULT";
        private const string IpdVisit = "IPD_VISIT";

        private readonly AppDbContext _context;

        public GetDoctorFeesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDoctorFeesResponseModel> Handle(GetDoctorFeesRequestModel request, CancellationToken cancellationToken)
        {
            // Doctors registered at THIS hospital are defined by the DoctorDepartment mapping
            // (a doctor is a global identity attached to one or more hospitals), NOT the single
            // retrofitted Doctor.HospitalId field. Distinct by DoctorID — a doctor may have several
            // department rows at the same hospital.
            var hospitalDoctorIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId == request.HospitalId)
                .Select(dd => dd.DoctorID)
                .Distinct()
                .ToListAsync(cancellationToken);

            var doctors = await _context.Doctors
                .Where(d => hospitalDoctorIds.Contains(d.DoctorID)
                         && _context.Users.Any(u => u.UserID == d.UserID
                                                 && u.UserStatusId != (int)UserStatusEnum.Revoked))
                .Select(d => new
                {
                    d.DoctorID,
                    d.UserID,
                    DepartmentName = d.PrimaryDepartment != null ? d.PrimaryDepartment.Name : null,
                    DoctorName = _context.UserProfiles
                        .Where(up => up.UserID == d.UserID)
                        .OrderByDescending(up => up.UpdatedAt)
                        .Select(up => up.FullName)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var fees = await _context.DoctorFees
                .Where(f => f.HospitalId == request.HospitalId)
                .ToListAsync(cancellationToken);

            var feeLookup = fees
                .GroupBy(f => f.DoctorId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var items = doctors
                .OrderBy(d => d.DoctorName)
                .Select(d =>
                {
                    feeLookup.TryGetValue(d.DoctorID, out var list);
                    decimal Of(string type) => list?.FirstOrDefault(f => f.FeeType == type)?.Amount ?? 0m;
                    return new DoctorFeeRow
                    {
                        DoctorId = d.DoctorID,
                        DoctorName = d.DoctorName,
                        DepartmentName = d.DepartmentName,
                        OpdConsultFee = Of(OpdConsult),
                        IpdVisitFee = Of(IpdVisit),
                    };
                })
                .ToList();

            return new GetDoctorFeesResponseModel { Items = items };
        }
    }
}
