using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
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
            var doctors = await _context.Doctors
                .Where(d => d.HospitalId == request.HospitalId)
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
