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
    /// Every medication order for an admission, newest first, with each line's current billing
    /// state (charged amount / voided) folded in so the order-entry screen can show at a glance
    /// what's been billed.
    /// </summary>
    public class GetMedicationOrdersHandler : IRequestHandler<GetMedicationOrdersRequestModel, GetMedicationOrdersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetMedicationOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetMedicationOrdersResponseModel> Handle(GetMedicationOrdersRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetMedicationOrdersResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var orders = await _context.ClinicalOrder
                    .Where(o => o.HospitalId == request.HospitalId
                        && o.AdmissionId == request.AdmissionId
                        && o.OrderType == IpdConstants.ClinicalOrderType.Medication)
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

                var linesByOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

                var items = orders.Select(o => new MedicationOrderItem
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
                            return new MedicationOrderLineItem
                            {
                                OrderLineId = l.OrderLineId,
                                DrugName = l.DrugName,
                                SaltName = l.SaltName,
                                Dose = l.Dose,
                                Route = l.Route,
                                Frequency = l.Frequency,
                                DurationDays = l.DurationDays,
                                Instructions = l.Instructions,
                                Qty = l.Qty,
                                StatusCode = l.StatusCode,
                                ChargeEventId = l.ChargeEventId,
                                ChargedAmount = charge?.NetAmount,
                                ChargeVoided = charge?.StatusCode == BillingConstants.ChargeEventStatus.Void,
                            };
                        }).ToList(),
                }).ToList();

                return new GetMedicationOrdersResponseModel { Success = true, Orders = items };
            }
            catch (Exception)
            {
                return new GetMedicationOrdersResponseModel { Success = false, Message = "Error loading medication orders." };
            }
        }
    }
}
