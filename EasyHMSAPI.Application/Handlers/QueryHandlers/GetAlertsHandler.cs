using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAlertsHandler : IRequestHandler<GetAlertsRequestModel, GetAlertsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAlertsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAlertsResponseModel> Handle(GetAlertsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Alert.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId);

                if (!string.IsNullOrWhiteSpace(request.Status))
                    query = query.Where(a => a.Status == request.Status);
                if (!string.IsNullOrWhiteSpace(request.Severity))
                    query = query.Where(a => a.Severity == request.Severity);
                if (!string.IsNullOrWhiteSpace(request.AlertCode))
                    query = query.Where(a => a.AlertCode == request.AlertCode);
                if (request.AdmissionId.HasValue)
                    query = query.Where(a => a.AdmissionId == request.AdmissionId);
                if (request.FromUtc.HasValue)
                    query = query.Where(a => a.RaisedAt >= request.FromUtc.Value);
                if (request.ToUtc.HasValue)
                    query = query.Where(a => a.RaisedAt <= request.ToUtc.Value);

                // Audience scoping: when a user/role is supplied, include alerts targeted to that
                // user or role, plus broadcast alerts (no specific audience).
                if (request.AudienceUserId.HasValue || !string.IsNullOrWhiteSpace(request.Role))
                {
                    var userId = request.AudienceUserId;
                    var role = request.Role;
                    query = query.Where(a =>
                        (a.AudienceUserId == null && a.AudienceRoles == null) ||
                        (userId != null && a.AudienceUserId == userId) ||
                        (role != null && a.AudienceRoles != null && a.AudienceRoles.Contains(role)));
                }

                var take = request.Take is > 0 ? request.Take.Value : 50;

                var items = await query
                    .OrderByDescending(a => a.RaisedAt)
                    .Take(take)
                    .Select(a => new AlertItem
                    {
                        AlertId = a.AlertId,
                        AlertCode = a.AlertCode,
                        Severity = a.Severity,
                        Title = a.Title,
                        Body = a.Body,
                        PatientId = a.PatientId,
                        AdmissionId = a.AdmissionId,
                        EncounterId = a.EncounterId,
                        AudienceRoles = a.AudienceRoles,
                        AudienceUserId = a.AudienceUserId,
                        AudienceWardCode = a.AudienceWardCode,
                        Status = a.Status,
                        RaisedAt = a.RaisedAt,
                        RaisedBy = a.RaisedBy,
                        SourceModule = a.SourceModule,
                        DispatchSms = a.DispatchSms,
                        DispatchWhatsApp = a.DispatchWhatsApp,
                        DispatchedAt = a.DispatchedAt,
                        DispatchError = a.DispatchError,
                        AcknowledgedAt = a.AcknowledgedAt,
                        AcknowledgedBy = a.AcknowledgedBy,
                        AcknowledgeNote = a.AcknowledgeNote,
                        DismissedAt = a.DismissedAt,
                        DismissedBy = a.DismissedBy,
                        DismissReason = a.DismissReason,
                        SnoozedUntil = a.SnoozedUntil,
                        PayloadJson = a.PayloadJson,
                    })
                    .ToListAsync(cancellationToken);

                return new GetAlertsResponseModel { Success = true, Items = items };
            }
            catch (Exception ex)
            {
                return new GetAlertsResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
