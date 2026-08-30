using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Every order of one OrderType for an admission, newest first, with each line's current
    /// billing state (charged amount / voided) folded in so the order-entry screen can show at a
    /// glance what's been billed. One generic handler for every CPOE tab (Medications/Lab/
    /// Radiology/Procedures/Diet/Nursing) — each queries its own OrderType.
    /// </summary>
    public class GetClinicalOrdersHandler : IRequestHandler<GetClinicalOrdersRequestModel, GetClinicalOrdersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetClinicalOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetClinicalOrdersResponseModel> Handle(GetClinicalOrdersRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetClinicalOrdersResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                var orderType = request.OrderType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(orderType) || !IpdConstants.ClinicalOrderType.All.Contains(orderType))
                    return new GetClinicalOrdersResponseModel { Success = false, Message = "Invalid order type." };

                var orders = await _context.ClinicalOrder
                    .Where(o => o.HospitalId == request.HospitalId
                        && o.AdmissionId == request.AdmissionId
                        && o.OrderType == orderType)
                    .OrderByDescending(o => o.OrderedAt)
                    .ToListAsync(cancellationToken);

                var orderIds = orders.Select(o => o.OrderId).ToList();
                var lines = await _context.ClinicalOrderLine
                    .Where(l => orderIds.Contains(l.OrderId))
                    .OrderBy(l => l.DisplayOrder)
                    .ToListAsync(cancellationToken);

                var chargeEventIds = lines.Where(l => l.ChargeEventId.HasValue).Select(l => l.ChargeEventId!.Value).Distinct().ToList();
                var chargesById = await _context.BillingChargeEvent
                    .Where(c => chargeEventIds.Contains(c.ChargeEventId))
                    .ToDictionaryAsync(c => c.ChargeEventId, cancellationToken);

                // Lab lines dual-written to a PathologyOrderLine (see ClinicalOrderCommandHandlers)
                // carry LinkedPathologyOrderLineId -- resolve each one's report status/number so the
                // panel can show "Completed (View Report)" once it's approved.
                var linkedPathologyLineIds = lines.Where(l => l.LinkedPathologyOrderLineId.HasValue)
                    .Select(l => l.LinkedPathologyOrderLineId!.Value).Distinct().ToList();
                var pathologyReportByLineId = linkedPathologyLineIds.Count == 0
                    ? new Dictionary<Guid, (string Status, Guid ReportId, string ReportNo)>()
                    : await (
                        from pol in _context.PathologyOrderLine
                        join rep in _context.PathologyReport on pol.ReportId equals rep.ReportId
                        where linkedPathologyLineIds.Contains(pol.OrderLineId) && pol.ReportId.HasValue
                        select new { pol.OrderLineId, rep.Status, rep.ReportId, rep.ReportNo }
                    ).ToDictionaryAsync(x => x.OrderLineId, x => (x.Status, x.ReportId, x.ReportNo), cancellationToken);

                var linesByOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

                var items = orders.Select(o => new ClinicalOrderItem
                {
                    OrderId = o.OrderId,
                    StatusCode = o.StatusCode,
                    OrderedAt = o.OrderedAt,
                    OrderedBy = o.OrderedBy,
                    Notes = o.Notes,
                    Lines = (linesByOrder.TryGetValue(o.OrderId, out var orderLines) ? orderLines : new())
                        .Select(l =>
                        {
                            BillingChargeEvent? charge = l.ChargeEventId.HasValue && chargesById.TryGetValue(l.ChargeEventId.Value, out var c) ? c : null;
                            var linkedReport = l.LinkedPathologyOrderLineId.HasValue
                                && pathologyReportByLineId.TryGetValue(l.LinkedPathologyOrderLineId.Value, out var pr)
                                ? pr : ((string Status, Guid ReportId, string ReportNo)?)null;
                            return new ClinicalOrderLineItem
                            {
                                OrderLineId = l.OrderLineId,
                                ItemName = l.ItemName,
                                SaltName = l.SaltName,
                                Dose = l.Dose,
                                Route = l.Route,
                                Frequency = l.Frequency,
                                DurationDays = l.DurationDays,
                                Instructions = l.Instructions,
                                Urgency = l.Urgency,
                                ScheduledAt = l.ScheduledAt,
                                IsHighAlert = l.IsHighAlert,
                                IsDailyRecurringCharge = l.IsDailyRecurringCharge,
                                Qty = l.Qty,
                                StatusCode = l.StatusCode,
                                ChargeEventId = l.ChargeEventId,
                                ChargedAmount = charge?.NetAmount,
                                ChargeVoided = charge?.StatusCode == BillingConstants.ChargeEventStatus.Void,
                                LinkedPathologyReportStatus = linkedReport?.Status,
                                LinkedPathologyReportId = linkedReport?.ReportId,
                                LinkedPathologyReportNo = linkedReport?.ReportNo,
                            };
                        }).ToList(),
                }).ToList();

                return new GetClinicalOrdersResponseModel { Success = true, Orders = items };
            }
            catch (Exception)
            {
                return new GetClinicalOrdersResponseModel { Success = false, Message = "Error loading orders." };
            }
        }
    }
}
