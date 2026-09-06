using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreatePathologyOrderHandler : IRequestHandler<CreatePathologyOrderRequestModel, CreatePathologyOrderResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;
        private readonly IUsageLimitService _usageLimitService;

        public CreatePathologyOrderHandler(AppDbContext context, IMediator mediator, IUsageLimitService usageLimitService)
        {
            _context = context;
            _mediator = mediator;
            _usageLimitService = usageLimitService;
        }

        // The DbContext is configured with EnableRetryOnFailure, so any user-initiated transaction
        // must run inside an execution strategy (as a retriable unit) -- same pattern as
        // CreateDraftInvoiceHandler. Concurrency conflicts (two orders racing for the same
        // NumberSeries/PathologyTokenQueue row) are handled at this outer level: clear the change
        // tracker and retry the WHOLE operation in a fresh transaction, rather than nesting a
        // second retry loop inside an already-open transaction.
        private const int MaxConcurrencyRetries = 3;

        public async Task<CreatePathologyOrderResponseModel> Handle(CreatePathologyOrderRequestModel request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
                }
                catch (DbUpdateException) when (attempt < MaxConcurrencyRetries - 1)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (Exception ex)
                {
                    return new CreatePathologyOrderResponseModel { Success = false, Message = ex.Message };
                }
            }

            return new CreatePathologyOrderResponseModel { Success = false, Message = "Order numbering contention. Please retry." };
        }

        private async Task<CreatePathologyOrderResponseModel> TryHandleAsync(CreatePathologyOrderRequestModel request, CancellationToken cancellationToken)
        {
            // A TestId not belonging to this hospital must never be accepted onto an order --
            // GetPathologyOrderByIdHandler resolves a line's test purely by TestId (now scoped by
            // HospitalId too, but this is the write-side half of that same fix), so letting a
            // foreign TestId through here would let this hospital read another hospital's private
            // catalog metadata back through their own order.
            var ownedTestCount = await _context.PathologyTestMaster
                .CountAsync(t => request.TestIds.Contains(t.TestId) && t.HospitalId == request.HospitalId, cancellationToken);
            if (ownedTestCount != request.TestIds.Distinct().Count())
            {
                return new CreatePathologyOrderResponseModel { Success = false, Message = "One or more selected tests are not in this hospital's catalog." };
            }

            // PatientId is a globally-unique identifier (not a per-hospital secret -- it routinely
            // appears on printed documents that leave the originating hospital), so without this
            // check a hospital could place an order against another hospital's patient and read
            // that patient's name/DOB/address/mobile back through GetPathologyOrderByIdHandler.
            // Same ownership-gate shape as the TestIds check above.
            var patientBelongsToHospital = await _context.PatientRegistrations
                .AnyAsync(p => p.PatientId == request.PatientId && p.HospitalId == request.HospitalId, cancellationToken);
            if (!patientBelongsToHospital)
            {
                return new CreatePathologyOrderResponseModel { Success = false, Message = "Patient not found in this hospital." };
            }

            var billingPolicy = await _context.BillingPolicy.FirstOrDefaultAsync(c => c.HospitalId == request.HospitalId, cancellationToken);
            bool autoBill = billingPolicy?.LabPathTrigger == "ON_ORDER";

            var now = DateTime.UtcNow;

            // Number series, token, and the order/lines themselves must commit or roll back
            // together -- previously each was its own separate SaveChangesAsync (token allocation
            // even had its own internal one inside PathologyTokenHelper), so a crash/disconnect
            // between any two of them left a permanently burned order number or token with no
            // corresponding order ever created. A gap, never a collision (both NumberSeries and
            // PathologyTokenQueue carry real concurrency protection), but still worth closing.
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                _context, request.HospitalId, BillingConstants.NumberSeriesCode.LabAccession, request.LoggedInUserName, cancellationToken);
            numberSeries.CurrentValue++;
            var orderNo = NumberSeriesFormatter.Format(
                numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
            numberSeries.UpdatedAt = now;
            numberSeries.UpdatedBy = request.LoggedInUserName;

            var tokenNumber = await PathologyTokenHelper.AllocateTokenWithLockingAsync(_context, request.HospitalId, now, cancellationToken);

            var order = new PathologyOrder
            {
                HospitalId = request.HospitalId,
                PatientId = request.PatientId,
                EncounterId = request.EncounterId,
                AdmissionId = request.AdmissionId,
                OrderedByDoctorId = request.OrderedByDoctorId,
                Notes = request.Notes,
                OrderNo = orderNo,
                OrderDate = DateTime.UtcNow,
                Status = "PLACED",
                SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "OPD" : request.SourceType,
                IsStat = request.IsStat,
                TokenNumber = tokenNumber,
                CreatedBy = request.LoggedInUserName
            };

            _context.PathologyOrder.Add(order);

            var orderLines = new List<PathologyOrderLine>();
            foreach (var testId in request.TestIds)
            {
                var orderLine = new PathologyOrderLine
                {
                    HospitalId = request.HospitalId,
                    OrderId = order.OrderId,
                    TestId = testId,
                    Status = "PENDING",
                    CreatedBy = request.LoggedInUserName
                };
                orderLines.Add(orderLine);
            }

            _context.PathologyOrderLine.AddRange(orderLines);
            await _context.SaveChangesAsync(cancellationToken);

            // Last gate before commit -- a free-tier hospital's monthly quota, atomically checked
            // and consumed together with this order inside the same transaction, so a limit
            // breach here rolls the whole order back too (including the number series bump).
            var usage = await _usageLimitService.TryConsumeAsync(request.HospitalId, cancellationToken);
            if (!usage.Allowed)
            {
                await tx.RollbackAsync(cancellationToken);
                return new CreatePathologyOrderResponseModel { Success = false, Message = usage.Message };
            }

            await tx.CommitAsync(cancellationToken);

            // Billing is deliberately OUTSIDE the transaction above -- a billing failure must not
            // undo an already-committed order (same soft-fail philosophy as
            // CollectPathologySampleHandler/GeneratePathologyReportHandler's billing dispatch).
            string? billingWarning = null;
            if (autoBill)
            {
                // IPD orders carry AdmissionId instead of EncounterId -- resolve the admission's
                // own encounter (same lookup ClinicalOrderCommandHandlers uses for bed/CPOE
                // charges) so an admission-only order still bills instead of silently skipping.
                var billingEncounterId = await PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync(
                    _context, request.HospitalId, request.EncounterId, request.AdmissionId, cancellationToken);

                if (billingEncounterId.HasValue)
                {
                    var charges = await PathologyAutoBillingHelper.BuildChargeDetailsAsync(
                        _context, request.HospitalId, request.TestIds, order.OrderId.ToString(), request.OrderedByDoctorId, cancellationToken);

                    if (charges.Any())
                    {
                        billingWarning = await PathologyAutoBillingHelper.PostChargesAndInvoiceAsync(
                            _mediator, request.HospitalId, request.PatientId, billingEncounterId.Value, charges,
                            request.LoggedInUserId, request.LoggedInUserName, "placed", cancellationToken);
                    }
                }
                else
                {
                    billingWarning = "Order placed, but auto-billing was skipped: no open visit/encounter to bill against. " +
                        "Add the charge manually from the Billing tab.";
                }
            }

            return new CreatePathologyOrderResponseModel
            {
                Success = true,
                OrderId = order.OrderId,
                OrderNo = orderNo,
                BillingWarning = billingWarning
            };
        }
    }
}
