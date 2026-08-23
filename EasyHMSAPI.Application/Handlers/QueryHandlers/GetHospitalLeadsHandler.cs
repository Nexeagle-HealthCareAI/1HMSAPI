using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalLeadsHandler : IRequestHandler<GetHospitalLeadsRequestModel, GetHospitalLeadsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalLeadsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalLeadsResponseModel> Handle(GetHospitalLeadsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new GetHospitalLeadsResponseModel { Success = false, Message = "Hospital ID is required." };

            var hospitalExists = await _context.Hospitals
                .AsNoTracking()
                .AnyAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
            if (!hospitalExists)
                return new GetHospitalLeadsResponseModel { Success = false, Message = "Hospital not found." };

            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 200);

            // Date-window-scoped, but not Source/LeadType-scoped -- see the response model's own
            // comment for why (these breakdowns must stay meaningful once the table is filtered).
            var windowQuery = _context.HospitalLeads.AsNoTracking()
                .Where(l => l.HospitalId == request.HospitalId);
            if (request.DateFrom.HasValue)
                windowQuery = windowQuery.Where(l => l.OccurredAt >= request.DateFrom.Value);
            if (request.DateTo.HasValue)
                windowQuery = windowQuery.Where(l => l.OccurredAt <= request.DateTo.Value);

            var countBySource = await windowQuery
                .GroupBy(l => l.Source)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var countByType = await windowQuery
                .GroupBy(l => l.LeadType)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

            var filteredQuery = windowQuery;
            if (!string.IsNullOrWhiteSpace(request.Source))
                filteredQuery = filteredQuery.Where(l => l.Source == request.Source);
            if (!string.IsNullOrWhiteSpace(request.LeadType))
                filteredQuery = filteredQuery.Where(l => l.LeadType == request.LeadType);

            var totalCount = await filteredQuery.CountAsync(cancellationToken);

            var pageRows = await filteredQuery
                .OrderByDescending(l => l.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var doctorIds = pageRows.Where(l => l.DoctorId.HasValue).Select(l => l.DoctorId!.Value).Distinct().ToList();
            var doctorNameById = doctorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await (
                    from d in _context.Doctors
                    where doctorIds.Contains(d.DoctorID)
                    join u in _context.Users on d.UserID equals u.UserID
                    join up in _context.UserProfiles on u.UserID equals up.UserID
                    select new { d.DoctorID, up.FullName }
                  ).ToDictionaryAsync(x => x.DoctorID, x => x.FullName, cancellationToken);

            var leads = pageRows.Select(l => new HospitalLeadInfo
            {
                LeadId = l.LeadId,
                DoctorId = l.DoctorId,
                DoctorName = l.DoctorId.HasValue && doctorNameById.TryGetValue(l.DoctorId.Value, out var name) ? name : null,
                Source = l.Source,
                LeadType = l.LeadType,
                SearchQuery = l.SearchQuery,
                Mobile = l.Mobile,
                PatientName = l.PatientName,
                Country = l.Country,
                Region = l.Region,
                City = l.City,
                OccurredAt = l.OccurredAt,
            }).ToList();

            return new GetHospitalLeadsResponseModel
            {
                Success = true,
                Leads = leads,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                CountBySource = countBySource,
                CountByType = countByType,
            };
        }
    }
}
