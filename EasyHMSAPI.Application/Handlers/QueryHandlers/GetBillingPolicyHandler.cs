using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBillingPolicyHandler : IRequestHandler<GetBillingPolicyRequestModel, GetBillingPolicyResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBillingPolicyHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBillingPolicyResponseModel> Handle(GetBillingPolicyRequestModel request, CancellationToken cancellationToken)
        {
            var policy = await _context.BillingPolicy
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

            if (policy == null)
            {
                return new GetBillingPolicyResponseModel
                {
                    Success = false,
                    Message = "Billing policy not found for the specified hospital."
                };
            }

            var series = await _context.NumberSeries
                .Where(s => s.HospitalId == request.HospitalId)
                .ToListAsync(cancellationToken);

            var data = new BillingPolicyDataModel
            {
                BillingPolicyId = policy.BillingPolicyId,
                HospitalId = policy.HospitalId,
                RequirePostBeforeInvoice = policy.RequirePostBeforeInvoice,
                MaxAutoDiscountPercent = policy.MaxAutoDiscountPercent,
                LabPathTrigger = policy.LabPathTrigger,
                LabRadTrigger = policy.LabRadTrigger,
                PharmacyIpdTrigger = policy.PharmacyIpdTrigger,
                OpdConsultTrigger = policy.OpdConsultTrigger,
                IpdBedChargeMode = policy.IpdBedChargeMode,
                SupplierGstin = policy.SupplierGstin,
                PlaceOfSupplyStateCode = policy.PlaceOfSupplyStateCode,
                DefaultPriceIsTaxInclusive = policy.DefaultPriceIsTaxInclusive,
                TaxRoundingMode = policy.TaxRoundingMode,
                UpdatedAt = policy.UpdatedAt,
                UpdatedBy = policy.UpdatedBy,
                NumberSeries = series.ToDictionary(
                    s => s.SeriesCode?.ToLower() ?? "unknown",
                    s => new NumberSeriesResponseModel
                    {
                        SeriesCode = s.SeriesCode,
                        Prefix = s.Prefix,
                        YearFormat = s.YearFormat,
                        Separator = s.Separator,
                        CurrentValue = s.CurrentValue,
                        PadLength = s.PadLength,
                        IsActive = s.IsActive
                    })
            };

            return new GetBillingPolicyResponseModel
            {
                Success = true,
                Message = "Billing policy retrieved successfully.",
                Data = data
            };
        }
    }
}
