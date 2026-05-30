using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertBillingPolicyHandler : IRequestHandler<UpsertBillingPolicyRequestModel, UpsertBillingPolicyResponseModel>
    {
        private const string SeriesInvoice = "INV";
        private const string SeriesReceipt = "RCPT";

        private readonly AppDbContext _context;

        public UpsertBillingPolicyHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertBillingPolicyResponseModel> Handle(UpsertBillingPolicyRequestModel request, CancellationToken cancellationToken)
        {
            var policy = await _context.BillingPolicy
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

            var now = DateTime.UtcNow;

            if (policy == null)
            {
                policy = new BillingPolicy
                {
                    BillingPolicyId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName
                };
                _context.BillingPolicy.Add(policy);
            }

            policy.LabPathTrigger = request.LabPathTrigger;
            policy.LabRadTrigger = request.LabRadTrigger;
            policy.PharmacyIpdTrigger = request.PharmacyIpdTrigger;
            policy.OpdConsultTrigger = request.OpdConsultTrigger;
            policy.IpdBedChargeMode = request.IpdBedChargeMode;
            policy.SupplierGstin = string.IsNullOrWhiteSpace(request.SupplierGstin) ? null : request.SupplierGstin.Trim().ToUpperInvariant();
            policy.PlaceOfSupplyStateCode = string.IsNullOrWhiteSpace(request.PlaceOfSupplyStateCode) ? null : request.PlaceOfSupplyStateCode.Trim();
            policy.DefaultPriceIsTaxInclusive = request.DefaultPriceIsTaxInclusive;
            policy.TaxRoundingMode = string.IsNullOrWhiteSpace(request.TaxRoundingMode) ? "ROUND" : request.TaxRoundingMode.Trim().ToUpperInvariant();
            policy.UpdatedAt = now;
            policy.UpdatedBy = request.LoggedInUserName;

            if (request.NumberSeries != null)
            {
                await UpsertNumberSeries(request.HospitalId, SeriesInvoice, request.NumberSeries.Invoice, request.LoggedInUserName, cancellationToken);
                await UpsertNumberSeries(request.HospitalId, SeriesReceipt, request.NumberSeries.Receipt, request.LoggedInUserName, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new UpsertBillingPolicyResponseModel
            {
                BillingPolicyId = policy.BillingPolicyId,
                UpdatedAt = policy.UpdatedAt,
                UpdatedBy = policy.UpdatedBy,
                NumberSeries = new NumberSeriesUpsertResponse
                {
                    Invoice = new NumberSeriesItemResponse { SeriesCode = SeriesInvoice },
                    Receipt = new NumberSeriesItemResponse { SeriesCode = SeriesReceipt }
                }
            };
        }

        private async Task UpsertNumberSeries(Guid hospitalId, string code, NumberSeriesItemUpdateModel? model, string? user, CancellationToken ct)
        {
            if (model == null) return;

            var series = await _context.NumberSeries
                .FirstOrDefaultAsync(s => s.HospitalId == hospitalId && s.SeriesCode == code, ct);

            var padLength = model.PadLength > 0 ? model.PadLength : 1;

            if (series == null)
            {
                series = new NumberSeries
                {
                    SeriesId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    SeriesCode = code,
                    CurrentValue = 0,
                    PadLength = padLength,
                    IsActive = model.IsActive,
                    Prefix = model.Prefix,
                    YearFormat = model.YearFormat,
                    Separator = model.Separator,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = user
                };
                _context.NumberSeries.Add(series);
            }
            else
            {
                series.Prefix = model.Prefix;
                series.YearFormat = model.YearFormat;
                series.Separator = model.Separator;
                series.PadLength = padLength;
                series.IsActive = model.IsActive;
                series.UpdatedAt = DateTime.UtcNow;
                series.UpdatedBy = user;
            }
        }
    }
}
