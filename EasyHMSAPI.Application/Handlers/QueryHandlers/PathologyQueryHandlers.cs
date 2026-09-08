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
                    TokenNumber = o.TokenNumber,
                    Notes = o.Notes,
                    // HospitalId included in both joins below even though PatientId alone is already
                    // globally unique -- defense-in-depth against any future patient-numbering
                    // scheme where that stops being true (see the pathology module audit).
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId && p.HospitalId == o.HospitalId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown",
                    PatientMobile = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId && p.HospitalId == o.HospitalId)
                        .Select(p => p.Mobile)
                        .FirstOrDefault(),
                    // Dashboard-list-only fields -- lets the Pathology Lab table show test count and
                    // how many of this order's tests have their own report ready, without a second
                    // round-trip per row. Each PathologyOrderLine now gets its own independent
                    // report (see GeneratePathologyReportHandler), so "one report per order" is no
                    // longer a valid assumption here -- count lines with a ReportId instead of
                    // picking an arbitrary single report.
                    TestCount = _context.PathologyOrderLine.Count(l => l.OrderId == o.OrderId),
                    ReportsReadyCount = _context.PathologyOrderLine.Count(l => l.OrderId == o.OrderId && l.ReportId != null)
                })
                .ToListAsync(cancellationToken);

            if (orders.Count == 0) return orders;

            // Test names and precise patient age are resolved in separate batched queries rather
            // than nested inside the projection above -- keeps the main query a simple, proven
            // scalar-subquery shape (same as TestCount/ReportsReadyCount) instead of relying on EF
            // translating a collection-valued subquery, and reuses PathologyAgeCalculator the same
            // way GetPathologyOrderByIdHandler already does for the single-order view.
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var testNamesByOrder = await _context.PathologyOrderLine
                .Where(l => orderIds.Contains(l.OrderId))
                .Join(_context.PathologyTestMaster, l => l.TestId, t => t.TestId, (l, t) => new { l.OrderId, t.TestName })
                .ToListAsync(cancellationToken);
            var testNamesLookup = testNamesByOrder
                .GroupBy(x => x.OrderId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TestName).ToList());

            var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();
            var dobByPatient = await _context.PatientRegistrations
                .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId) && p.HospitalId == request.HospitalId)
                .Select(p => new { p.PatientId, p.DateOfBirth })
                .ToListAsync(cancellationToken);
            var dobLookup = dobByPatient.ToDictionary(p => p.PatientId!, p => p.DateOfBirth);

            foreach (var order in orders)
            {
                if (testNamesLookup.TryGetValue(order.OrderId, out var names)) order.TestNames = names;
                if (dobLookup.TryGetValue(order.PatientId, out var dob)) order.PatientAgeYears = PathologyAgeCalculator.CalculateAgeYears(dob);
            }

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
                    TokenNumber = o.TokenNumber,
                    Notes = o.Notes,
                    EncounterId = o.EncounterId,
                    AdmissionId = o.AdmissionId,
                    ReportFieldValuesJson = o.ReportFieldValuesJson,
                    // HospitalId included in every join below even though PatientId alone is already
                    // globally unique -- defense-in-depth against any future patient-numbering
                    // scheme where that stops being true (see the pathology module audit).
                    PatientName = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId && p.HospitalId == o.HospitalId)
                        .Select(p => p.FullName)
                        .FirstOrDefault() ?? "Unknown",
                    PatientGender = _context.PatientRegistrations
                        .Where(p => p.PatientId == o.PatientId && p.HospitalId == o.HospitalId)
                        .Select(p => p.Sex)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null) return new PathologyOrderDto();

            var patientDob = await _context.PatientRegistrations
                .Where(p => p.PatientId == order.PatientId && p.HospitalId == request.HospitalId)
                .Select(p => p.DateOfBirth)
                .FirstOrDefaultAsync(cancellationToken);
            order.PatientAgeYears = PathologyAgeCalculator.CalculateAgeYears(patientDob);

            var patientAddressParts = await _context.PatientRegistrations
                .Where(p => p.PatientId == order.PatientId && p.HospitalId == request.HospitalId)
                .Select(p => new { p.AddressLine, p.City, p.State, p.Pincode })
                .FirstOrDefaultAsync(cancellationToken);
            if (patientAddressParts != null)
            {
                var line = patientAddressParts.AddressLine?.Trim();
                var cityState = string.Join(", ", new[] { patientAddressParts.City, patientAddressParts.State }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                var tail = string.IsNullOrWhiteSpace(patientAddressParts.Pincode)
                    ? cityState
                    : string.IsNullOrWhiteSpace(cityState) ? patientAddressParts.Pincode : $"{cityState} - {patientAddressParts.Pincode}";
                order.PatientAddress = string.Join(", ", new[] { line, tail }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(order.PatientAddress)) order.PatientAddress = null;
            }

            var orderedByDoctorId = await _context.PathologyOrder
                .Where(o => o.HospitalId == request.HospitalId && o.OrderId == request.OrderId)
                .Select(o => o.OrderedByDoctorId)
                .FirstOrDefaultAsync(cancellationToken);
            if (orderedByDoctorId.HasValue)
            {
                order.OrderedByDoctorName = await _context.Doctors
                    .Where(d => d.DoctorID == orderedByDoctorId.Value)
                    .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            order.HospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == request.HospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var lines = await _context.PathologyOrderLine
                .Where(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);

            var externalLabIds = lines.Where(l => l.ExternalLabId.HasValue).Select(l => l.ExternalLabId!.Value).Distinct().ToList();
            var externalLabNamesById = externalLabIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.PathologyExternalLab
                    .Where(x => x.HospitalId == request.HospitalId && externalLabIds.Contains(x.ExternalLabId))
                    .ToDictionaryAsync(x => x.ExternalLabId, x => x.LabName, cancellationToken);

            foreach (var line in lines)
            {
                var test = await _context.PathologyTestMaster
                    .Where(t => t.TestId == line.TestId && t.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                var result = await _context.PathologyResult
                    .Where(r => r.OrderLineId == line.OrderLineId && r.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                // Each line now owns its own report (see GeneratePathologyReportHandler) rather than
                // sharing one report for the whole order -- resolved per line via line.ReportId.
                PathologyReportDto? lineReport = null;
                if (line.ReportId.HasValue)
                {
                    var report = await _context.PathologyReport
                        .Where(r => r.ReportId == line.ReportId.Value && r.HospitalId == request.HospitalId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (report != null)
                    {
                        lineReport = new PathologyReportDto
                        {
                            ReportId = report.ReportId,
                            ReportNo = report.ReportNo,
                            Status = report.Status,
                            GeneratedAt = report.GeneratedAt,
                            PdfBlobPath = report.PdfBlobPath,
                            PdfSha256 = report.PdfSha256,
                        };
                    }
                }

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
                    IsOutsourced = test?.IsOutsourced ?? false,
                    DefaultExternalLabId = test?.DefaultExternalLabId,
                    ExternalLabId = line.ExternalLabId,
                    ExternalLabName = line.ExternalLabId.HasValue && externalLabNamesById.TryGetValue(line.ExternalLabId.Value, out var labName) ? labName : null,
                    SentToExternalLabAt = line.SentToExternalLabAt,
                    ExternalLabRefNo = line.ExternalLabRefNo,
                    ExternalLabReceivedAt = line.ExternalLabReceivedAt,
                    ExternalLabCost = line.ExternalLabCost,
                    Result = result == null ? null : new PathologyResultDto
                    {
                        ResultId = result.ResultId,
                        ResultValuesJson = result.ResultValuesJson,
                        Interpretation = result.Interpretation
                    },
                    Report = lineReport
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

            // Newest first -- a patient can now genuinely have multiple *different* reports in the
            // window (one report per test line rather than one per order), so the frontend must keep
            // all of them rather than assuming one-per-patient. TestName (via the line each report
            // belongs to) lets the UI tell them apart.
            return await (
                from report in _context.PathologyReport
                join order in _context.PathologyOrder on report.OrderId equals order.OrderId
                join line in _context.PathologyOrderLine on report.ReportId equals line.ReportId into lineJoin
                from line in lineJoin.DefaultIfEmpty()
                join test in _context.PathologyTestMaster on line.TestId equals test.TestId into testJoin
                from test in testJoin.DefaultIfEmpty()
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
                    TestName = test != null ? test.TestName : null,
                }
            ).ToListAsync(cancellationToken);
        }
    }
}
