using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPathologyOrdersHandler : IRequestHandler<GetPathologyOrdersQuery, List<PathologyOrderDto>>
    {
        private readonly AppDbContext _context;

        public GetPathologyOrdersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PathologyOrderDto>> Handle(GetPathologyOrdersQuery request, CancellationToken cancellationToken)
        {
            var query = _context.PathologyOrder
                .Where(o => o.HospitalId == request.HospitalId);

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(o => o.Status == request.Status);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new PathologyOrderDto
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PatientId = o.PatientId,
                    SourceType = o.SourceType,
                    IsStat = o.IsStat,
                    // Get patient name if possible, assuming PatientRegistration is joined
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync(cancellationToken);

            return orders;
        }
    }

    public class GetPathologyOrderByIdHandler : IRequestHandler<GetPathologyOrderByIdQuery, PathologyOrderDto>
    {
        private readonly AppDbContext _context;

        public GetPathologyOrderByIdHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PathologyOrderDto> Handle(GetPathologyOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var order = await _context.PathologyOrder
                .Where(o => o.HospitalId == request.HospitalId && o.OrderId == request.OrderId)
                .Select(o => new PathologyOrderDto
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PatientId = o.PatientId,
                    SourceType = o.SourceType,
                    IsStat = o.IsStat,
                    EncounterId = o.EncounterId,
                    ReportFieldValuesJson = o.ReportFieldValuesJson,
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown",
                    PatientGender = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId)
                        .Select(p => p.Sex)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null) return new PathologyOrderDto();

            var patientDob = await _context.PatientRegistrations
                .Where(p => p.PatientId == order.PatientId)
                .Select(p => p.DateOfBirth)
                .FirstOrDefaultAsync(cancellationToken);
            order.PatientAgeYears = PathologyAgeCalculator.CalculateAgeYears(patientDob);

            order.HospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == request.HospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var report = await _context.PathologyReport
                .Where(r => r.HospitalId == request.HospitalId && r.OrderId == request.OrderId)
                .FirstOrDefaultAsync(cancellationToken);
            if (report != null)
            {
                order.Report = new PathologyReportDto
                {
                    ReportId = report.ReportId,
                    ReportNo = report.ReportNo,
                    Status = report.Status,
                    GeneratedAt = report.GeneratedAt,
                    PdfBlobPath = report.PdfBlobPath,
                    PdfSha256 = report.PdfSha256,
                };
            }

            var lines = await _context.PathologyOrderLine
                .Where(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);

            foreach (var line in lines)
            {
                var test = await _context.PathologyTestMaster
                    .Where(t => t.TestId == line.TestId)
                    .FirstOrDefaultAsync(cancellationToken);
                    
                var result = await _context.PathologyResult
                    .Where(r => r.OrderLineId == line.OrderLineId)
                    .FirstOrDefaultAsync(cancellationToken);

                order.Lines.Add(new PathologyOrderLineDto
                {
                    OrderLineId = line.OrderLineId,
                    TestId = line.TestId,
                    TestName = test?.TestName ?? "Unknown Test",
                    TestCode = test?.TestCode ?? "Unknown Code",
                    Status = line.Status,
                    ParameterSchemaJson = test?.ParameterSchemaJson,
                    SampleBarcode = line.SampleBarcode,
                    SampleCollectedAt = line.SampleCollectedAt,
                    Result = result == null ? null : new PathologyResultDto
                    {
                        ResultId = result.ResultId,
                        ResultValuesJson = result.ResultValuesJson,
                        Interpretation = result.Interpretation
                    }
                });
            }

            return order;
        }
    }

    public class GetRecentlyApprovedPathologyReportsHandler : IRequestHandler<GetRecentlyApprovedPathologyReportsQuery, List<PathologyReportReadyDto>>
    {
        private readonly AppDbContext _context;

        public GetRecentlyApprovedPathologyReportsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PathologyReportReadyDto>> Handle(GetRecentlyApprovedPathologyReportsQuery request, CancellationToken cancellationToken)
        {
            // 30 days is a generous window for "still worth flagging on the board" -- a report
            // generated that long ago has almost certainly already been seen by the ordering doctor.
            // There's no separate "approved" milestone anymore (the sign-off workflow was removed),
            // so every generated report qualifies -- GeneratedAt both filters the window and orders
            // the result.
            var since = DateTime.UtcNow.AddDays(-30);

            // Newest first -- a patient can have multiple reports in the window, and the frontend
            // indexes this list by patientId keeping only the first one seen per patient (same
            // "ordered desc, first-seen wins" convention as referralsByPatient in DocBoard.tsx), so
            // the ordering here is what actually decides which report wins.
            return await (
                from report in _context.PathologyReport
                join order in _context.PathologyOrder on report.OrderId equals order.OrderId
                where report.HospitalId == request.HospitalId
                    && report.GeneratedAt >= since
                orderby report.GeneratedAt descending
                select new PathologyReportReadyDto
                {
                    PatientId = order.PatientId,
                    ReportId = report.ReportId,
                    ReportNo = report.ReportNo,
                    OrderNo = order.OrderNo,
                    GeneratedAt = report.GeneratedAt,
                    PdfBlobPath = report.PdfBlobPath,
                }
            ).ToListAsync(cancellationToken);
        }
    }
}
