using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Fills the gap PathologyOrderLine.Status always had room for (PENDING -> SAMPLE_COLLECTED ->
    // RESULT_ENTERED) but nothing ever exercised -- EnterPathologyResultHandler has always accepted
    // PENDING directly, so collecting a sample stays optional bookkeeping, not a gate results entry
    // depends on.
    public class CollectPathologySampleHandler : IRequestHandler<CollectPathologySampleCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public CollectPathologySampleHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<bool> Handle(CollectPathologySampleCommand request, CancellationToken cancellationToken)
        {
            var line = await _context.PathologyOrderLine
                .FirstOrDefaultAsync(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId && l.OrderLineId == request.OrderLineId, cancellationToken);
            if (line == null || line.Status != "PENDING")
            {
                return false;
            }

            var order = await _context.PathologyOrder
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);
            // A cancelled order's lines must stay frozen -- nothing downstream (result entry, report
            // generation) re-checks this, so this is the one place that can still stop a cancelled
            // order from quietly resuming processing (and re-billing) after cancellation.
            if (order == null || order.Status == "CANCELLED")
            {
                return false;
            }

            var now = DateTime.UtcNow;
            line.Status = "SAMPLE_COLLECTED";
            line.SampleCollectedAt = now;
            if (!string.IsNullOrWhiteSpace(request.SampleBarcode))
            {
                line.SampleBarcode = request.SampleBarcode.Trim();
            }
            line.UpdatedAt = now;
            line.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            _context.PathologyOrderLine.Update(line);

            if (order.Status == "PLACED")
            {
                order.Status = "IN_PROGRESS";
                order.UpdatedAt = now;
                order.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
                _context.PathologyOrder.Update(order);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var billingPolicy = await _context.BillingPolicy
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
            if (billingPolicy?.LabPathTrigger == "ON_SAMPLE_COLLECTION" && order != null)
            {
                await DispatchSampleCollectionBillingAsync(order, line.TestId, request, cancellationToken);
            }

            return true;
        }

        // Best-effort, same as ApprovePathologyReportHandler's ON_REPORT_APPROVAL dispatch -- the
        // sample is already marked collected by this point and that must not be undone by a
        // billing hiccup.
        private async Task DispatchSampleCollectionBillingAsync(
            PathologyOrder order, Guid testId, CollectPathologySampleCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var billingEncounterId = await PathologyAutoBillingHelper.ResolveBillingEncounterIdAsync(
                    _context, request.HospitalId, order.EncounterId, order.AdmissionId, cancellationToken);
                if (!billingEncounterId.HasValue) return;

                var charges = await PathologyAutoBillingHelper.BuildChargeDetailsAsync(
                    _context, request.HospitalId, new[] { testId }, order.OrderId.ToString(), order.OrderedByDoctorId, cancellationToken);
                if (!charges.Any()) return;

                await _mediator.Send(new AddChargeEventRequestModel
                {
                    HospitalId = request.HospitalId,
                    PatientId = order.PatientId,
                    EncounterId = billingEncounterId.Value,
                    Charges = charges,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);
            }
            catch
            {
                // Swallow -- sample collection already succeeded and must not be undone by a billing failure.
            }
        }
    }
}
