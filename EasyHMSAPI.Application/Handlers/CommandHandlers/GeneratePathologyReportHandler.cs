using System;
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

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GeneratePathologyReportHandler : IRequestHandler<GeneratePathologyReportCommand, GeneratePathologyReportResponseModel>
    {
        private readonly AppDbContext _context;

        public GeneratePathologyReportHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GeneratePathologyReportResponseModel> Handle(GeneratePathologyReportCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Validate the order exists and belongs to this hospital
                var order = await _context.PathologyOrder
                    .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId, cancellationToken);

                if (order == null)
                {
                    return new GeneratePathologyReportResponseModel { Success = false, Message = "Order not found." };
                }

                // 2. Validate all order lines have results entered
                var orderLines = await _context.PathologyOrderLine
                    .Where(l => l.OrderId == request.OrderId && l.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (!orderLines.Any())
                {
                    return new GeneratePathologyReportResponseModel { Success = false, Message = "No test lines found for this order." };
                }

                var lineIds = orderLines.Select(l => l.OrderLineId).ToList();
                var results = await _context.PathologyResult
                    .Where(r => lineIds.Contains(r.OrderLineId) && r.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                var linesWithResults = results.Select(r => r.OrderLineId).ToHashSet();
                var linesWithoutResults = orderLines.Where(l => !linesWithResults.Contains(l.OrderLineId)).ToList();

                if (linesWithoutResults.Any())
                {
                    return new GeneratePathologyReportResponseModel
                    {
                        Success = false,
                        Message = $"Results have not been entered for {linesWithoutResults.Count} test(s). Please enter all results before generating a report."
                    };
                }

                // 3. Check if a report already exists for this order
                var existingReport = await _context.PathologyReport
                    .FirstOrDefaultAsync(r => r.OrderId == request.OrderId && r.HospitalId == request.HospitalId, cancellationToken);

                if (existingReport != null)
                {
                    return new GeneratePathologyReportResponseModel
                    {
                        Success = false,
                        Message = $"A report (#{existingReport.ReportNo}) already exists for this order."
                    };
                }

                // 4. Generate report number
                string reportNo = string.Empty;
                var now = DateTime.UtcNow;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                            _context, request.HospitalId, BillingConstants.NumberSeriesCode.LabReport, request.LoggedInUserName, cancellationToken);
                        numberSeries.CurrentValue++;
                        reportNo = NumberSeriesFormatter.Format(
                            numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                        numberSeries.UpdatedAt = now;
                        numberSeries.UpdatedBy = request.LoggedInUserName;
                        break;
                    }
                    catch (DbUpdateException)
                    {
                        _context.ChangeTracker.Clear();
                        if (attempt == 4) throw;
                    }
                }

                // 5. Resolve template — use provided, else fall back to hospital default
                Guid? templateId = request.TemplateId;
                if (!templateId.HasValue)
                {
                    var defaultTemplate = await _context.PathologyReportTemplate
                        .FirstOrDefaultAsync(t => t.HospitalId == request.HospitalId && t.IsDefault && t.IsActive, cancellationToken);
                    templateId = defaultTemplate?.TemplateId;
                }

                // 6. Create the report record
                var report = new PathologyReport
                {
                    ReportId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    OrderId = request.OrderId,
                    TemplateId = templateId,
                    ReportNo = reportNo,
                    Status = "DRAFT",
                    GeneratedAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName ?? "System",
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName ?? "System"
                };
                _context.PathologyReport.Add(report);

                // 7. Link all results to this report
                foreach (var result in results)
                {
                    result.ReportId = report.ReportId;
                    result.UpdatedAt = now;
                    result.UpdatedBy = request.LoggedInUserName ?? "System";
                    _context.PathologyResult.Update(result);
                }

                // 8. Update order status
                if (order.Status != "COMPLETED")
                {
                    order.Status = "COMPLETED";
                    order.UpdatedAt = now;
                    order.UpdatedBy = request.LoggedInUserName ?? "System";
                    _context.PathologyOrder.Update(order);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new GeneratePathologyReportResponseModel
                {
                    Success = true,
                    ReportId = report.ReportId,
                    ReportNo = reportNo
                };
            }
            catch (Exception ex)
            {
                return new GeneratePathologyReportResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
