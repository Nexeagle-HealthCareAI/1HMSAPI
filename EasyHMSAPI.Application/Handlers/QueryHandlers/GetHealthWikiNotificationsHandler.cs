using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHealthWikiNotificationsHandler : IRequestHandler<GetHealthWikiNotificationsRequestModel, GetHealthWikiNotificationsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHealthWikiNotificationsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHealthWikiNotificationsResponseModel> Handle(GetHealthWikiNotificationsRequestModel request, CancellationToken ct)
        {
            var take = Math.Clamp(request.Take, 1, 200);
            var query = _context.HealthWikiNotifications.AsNoTracking();
            if (request.ProblemsOnly)
                query = query.Where(n => n.Status != HealthWikiNotification.StatusSent || n.DeliveryStatus == HealthWikiNotification.DeliveryFailed);

            var rows = await query.OrderByDescending(n => n.CreatedAt).Take(take)
                .Select(n => new HealthWikiNotificationInfo
                {
                    Code = n.Code, TemplateName = n.TemplateName, RecipientMobileMasked = n.RecipientMobileMasked, EntityType = n.EntityType,
                    EntityId = n.EntityId, Status = n.Status, Detail = n.Detail, DeliveryStatus = n.DeliveryStatus, DeliveryDetail = n.DeliveryDetail,
                    CreatedAt = n.CreatedAt, DeliveryAt = n.DeliveryAt,
                }).ToListAsync(ct);
            return new GetHealthWikiNotificationsResponseModel { Notifications = rows };
        }
    }
}
