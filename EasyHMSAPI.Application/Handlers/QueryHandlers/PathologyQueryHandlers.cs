using MediatR;
using Microsoft.EntityFrameworkCore;
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
                    TechnicianName = report.TechnicianName,
                    TechnicianRegNo = report.TechnicianRegNo,
                    TechnicianSignedAt = report.TechnicianSignedAt,
                    PathologistName = report.PathologistName,
                    PathologistRegNo = report.PathologistRegNo,
                    ApprovedAt = report.ApprovedAt,
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

    public class GetPathologyReportVerificationHandler : IRequestHandler<GetPathologyReportVerificationQuery, PathologyReportVerificationResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPathologyReportVerificationHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PathologyReportVerificationResponseModel> Handle(GetPathologyReportVerificationQuery request, CancellationToken cancellationToken)
        {
            var report = await _context.PathologyReport
                .FirstOrDefaultAsync(r => r.ReportId == request.ReportId, cancellationToken);

            if (report == null)
            {
                return new PathologyReportVerificationResponseModel { IsAuthentic = false, Message = "No report found for this code." };
            }

            if (report.Status != "APPROVED")
            {
                return new PathologyReportVerificationResponseModel { IsAuthentic = false, Message = "This report has not been finalized and approved." };
            }

            // The QR embedded in the PDF itself can only encode the reportId -- the hash can't be
            // known until the PDF (QR included) has finished rendering, so it can never be baked
            // into its own QR payload. A bare QR scan therefore does the basic existence+approved
            // check below. Supplying ?hash= (e.g. typed in from a "Document Hash" line printed
            // separately on the report) upgrades this to a strict byte-for-byte tamper check.
            var providedHash = (request.Sha256 ?? "").Trim();
            if (!string.IsNullOrEmpty(providedHash))
            {
                if (string.IsNullOrEmpty(report.PdfSha256) || !string.Equals(providedHash, report.PdfSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new PathologyReportVerificationResponseModel
                    {
                        IsAuthentic = false,
                        Message = "This document's content does not match our records. It may have been altered after issue."
                    };
                }
            }

            var hospitalName = await _context.Hospitals
                .Where(h => h.HospitalID == report.HospitalId)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return new PathologyReportVerificationResponseModel
            {
                IsAuthentic = true,
                Message = string.IsNullOrEmpty(providedHash)
                    ? "This report was genuinely issued by this hospital. Enter the document hash for a stricter tamper check."
                    : "This is a genuine, unaltered report.",
                ReportNo = report.ReportNo,
                HospitalName = hospitalName,
                ApprovedAt = report.ApprovedAt,
                TechnicianName = report.TechnicianName,
                PathologistName = report.PathologistName,
            };
        }
    }
}
