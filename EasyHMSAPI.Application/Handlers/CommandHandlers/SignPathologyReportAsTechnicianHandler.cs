using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SignPathologyReportAsTechnicianHandler : IRequestHandler<SignPathologyReportAsTechnicianCommand, bool>
    {
        private readonly AppDbContext _context;

        public SignPathologyReportAsTechnicianHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(SignPathologyReportAsTechnicianCommand request, CancellationToken cancellationToken)
        {
            var report = await _context.PathologyReport
                .FirstOrDefaultAsync(r => r.ReportId == request.ReportId && r.HospitalId == request.HospitalId, cancellationToken);

            if (report == null)
            {
                throw new ApplicationException("Pathology report not found.");
            }

            if (report.Status != "DRAFT")
            {
                throw new ApplicationException(report.Status == "TECH_SIGNED" || report.Status == "APPROVED"
                    ? "This report has already been signed by a technician."
                    : $"Report is in an unexpected state ({report.Status}) for technician sign-off.");
            }

            if (string.IsNullOrWhiteSpace(request.TechnicianRegNo))
            {
                throw new ApplicationException("A technician registration number (DMLT/BMLT) is required to sign this report.");
            }

            var now = DateTime.UtcNow;
            report.Status = "TECH_SIGNED";
            report.TechnicianUserId = request.LoggedInUserId;
            report.TechnicianName = request.LoggedInUserName;
            report.TechnicianRegNo = request.TechnicianRegNo.Trim();
            report.TechnicianSignedAt = now;
            report.UpdatedAt = now;
            report.UpdatedBy = request.LoggedInUserName ?? "System";
            _context.PathologyReport.Update(report);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
