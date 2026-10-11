using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Records what WhatsApp later said about a Health Wiki message it had accepted. Rules: HealthWikiDeliveryRules.</summary>
    public class RecordHealthWikiDeliveryHandler : IRequestHandler<RecordHealthWikiDeliveryRequestModel, RecordHealthWikiDeliveryResponseModel>
    {
        private readonly AppDbContext _context;

        public RecordHealthWikiDeliveryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordHealthWikiDeliveryResponseModel> Handle(RecordHealthWikiDeliveryRequestModel request, CancellationToken ct)
        {
            var id = request.MessageId?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > 150)
                return new RecordHealthWikiDeliveryResponseModel { Success = false, StatusCode = 400, Message = "A message id is required." };

            // Statuses such as "sent" carry nothing new; answer fine so the webhook does not retry.
            var status = HealthWikiDeliveryRules.Normalize(request.Status);
            if (status == null) return new RecordHealthWikiDeliveryResponseModel { Success = true };

            var row = await _context.HealthWikiNotifications.FirstOrDefaultAsync(n => n.ProviderMessageId == id, ct);
            if (row == null) return new RecordHealthWikiDeliveryResponseModel { Success = true, Matched = false };

            if (!HealthWikiDeliveryRules.ShouldApply(row.DeliveryStatus, status))
                return new RecordHealthWikiDeliveryResponseModel { Success = true, Matched = true, Applied = false };

            row.DeliveryStatus = status;
            row.DeliveryDetail = status == Domain.Entities.HealthWikiNotification.DeliveryFailed ? HealthWikiDeliveryRules.Detail(request.Detail) : null;
            row.DeliveryAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return new RecordHealthWikiDeliveryResponseModel { Success = true, Matched = true, Applied = true };
        }
    }
}
