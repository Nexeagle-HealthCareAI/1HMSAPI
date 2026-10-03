using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class VerifyPathologyReportHandler : IRequestHandler<VerifyPathologyReportCommand, VerifyPathologyReportResult>
    {
        private readonly AppDbContext _context;

        public VerifyPathologyReportHandler(AppDbContext context) => _context = context;

        public async Task<VerifyPathologyReportResult> Handle(VerifyPathologyReportCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PathologistName) || string.IsNullOrWhiteSpace(request.PathologistRegNo))
                return new VerifyPathologyReportResult { Success = false, Message = "The verifying pathologist's name and registration number are required." };

            var report = await _context.PathologyReport
                .FirstOrDefaultAsync(r => r.ReportId == request.ReportId && r.OrderId == request.OrderId && r.HospitalId == request.HospitalId, cancellationToken);
            if (report == null)
                return new VerifyPathologyReportResult { Success = false, Message = "Report not found." };

            var order = await _context.PathologyOrder
                .Where(o => o.OrderId == request.OrderId && o.HospitalId == request.HospitalId)
                .Select(o => o.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (order == "CANCELLED")
                return new VerifyPathologyReportResult { Success = false, Message = "The order was cancelled." };
            if (report.Status == "VERIFIED")
                return new VerifyPathologyReportResult { Success = false, Message = "This report is already verified." };

            var now = DateTime.UtcNow;
            report.Status = "VERIFIED";
            report.ApprovedAt = now;
            report.ApprovedByUserId = request.LoggedInUserId == Guid.Empty ? null : request.LoggedInUserId;
            report.PathologistName = request.PathologistName.Trim();
            report.PathologistRegNo = request.PathologistRegNo.Trim();
            report.PathologistDoctorId = request.PathologistDoctorId;
            report.UpdatedAt = now;
            report.UpdatedBy = request.LoggedInUserName ?? request.LoggedInUserId.ToString();
            await _context.SaveChangesAsync(cancellationToken);

            return new VerifyPathologyReportResult { Success = true, Message = "Report verified." };
        }
    }
}
