using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsultantIncentiveSummaryHandler : IRequestHandler<GetConsultantIncentiveSummaryRequestModel, GetConsultantIncentiveSummaryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsultantIncentiveSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsultantIncentiveSummaryResponseModel> Handle(GetConsultantIncentiveSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetConsultantIncentiveSummaryResponseModel { Success = false, Message = "HospitalId is required." };

                var rows = await _context.ConsultantIncentiveLedger
                    .Where(c => c.HospitalId == request.HospitalId)
                    .GroupBy(c => c.DoctorId)
                    .Select(g => new
                    {
                        DoctorId = g.Key,
                        AccruedTotal = g.Where(x => x.StatusCode == "ACCRUED").Sum(x => (decimal?)x.IncentiveAmount) ?? 0,
                        PaidTotal = g.Where(x => x.StatusCode == "PAID").Sum(x => (decimal?)x.IncentiveAmount) ?? 0,
                        CancelledTotal = g.Where(x => x.StatusCode == "CANCELLED").Sum(x => (decimal?)x.IncentiveAmount) ?? 0,
                    })
                    .ToListAsync(cancellationToken);

                var doctorIds = rows.Select(r => r.DoctorId).ToList();
                var userIdByDoctor = await _context.Doctors
                    .Where(d => doctorIds.Contains(d.DoctorID))
                    .Select(d => new { d.DoctorID, d.UserID })
                    .ToDictionaryAsync(d => d.DoctorID, d => d.UserID, cancellationToken);

                var userIds = userIdByDoctor.Values.Distinct().ToList();
                var nameByUser = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameLookup = nameByUser
                    .GroupBy(n => n.UserID)
                    .ToDictionary(g => g.Key, g => g.First().FullName);

                var doctors = rows.Select(r => new ConsultantIncentiveDoctorSummary
                {
                    DoctorId = r.DoctorId,
                    DoctorName = userIdByDoctor.TryGetValue(r.DoctorId, out var uid) && nameLookup.TryGetValue(uid, out var n) ? n : null,
                    AccruedTotal = r.AccruedTotal,
                    PaidTotal = r.PaidTotal,
                    CancelledTotal = r.CancelledTotal,
                })
                .OrderByDescending(d => d.AccruedTotal)
                .ToList();

                return new GetConsultantIncentiveSummaryResponseModel { Success = true, Doctors = doctors };
            }
            catch (Exception)
            {
                return new GetConsultantIncentiveSummaryResponseModel { Success = false, Message = "Error loading the consultant incentive summary." };
            }
        }
    }
}
