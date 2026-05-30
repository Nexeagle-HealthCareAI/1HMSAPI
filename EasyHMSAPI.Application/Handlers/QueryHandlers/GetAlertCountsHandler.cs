using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAlertCountsHandler : IRequestHandler<GetAlertCountsRequestModel, GetAlertCountsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAlertCountsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAlertCountsResponseModel> Handle(GetAlertCountsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Alert.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId && a.Status == "ACTIVE");

                // Same audience scoping as GetAlerts: targeted to user/role, or broadcast.
                if (request.AudienceUserId.HasValue || !string.IsNullOrWhiteSpace(request.Role))
                {
                    var userId = request.AudienceUserId;
                    var role = request.Role;
                    query = query.Where(a =>
                        (a.AudienceUserId == null && a.AudienceRoles == null) ||
                        (userId != null && a.AudienceUserId == userId) ||
                        (role != null && a.AudienceRoles != null && a.AudienceRoles.Contains(role)));
                }

                var bySeverity = await query
                    .GroupBy(a => a.Severity)
                    .Select(g => new { Severity = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken);

                int CountFor(string sev) => bySeverity.FirstOrDefault(x => x.Severity == sev)?.Count ?? 0;

                return new GetAlertCountsResponseModel
                {
                    Success = true,
                    ActiveInfo = CountFor("INFO"),
                    ActiveWarning = CountFor("WARNING"),
                    ActiveCritical = CountFor("CRITICAL"),
                    ActiveTotal = bySeverity.Sum(x => x.Count),
                };
            }
            catch (Exception ex)
            {
                return new GetAlertCountsResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
