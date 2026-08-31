using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Called by the frontend right after GeneratePathologyReportHandler succeeds, once it has
    // rendered the report PDF client-side (see generatePathologyReportPdf.ts). Freely re-callable,
    // same as generation itself -- re-uploading just overwrites the previous blob/hash so the
    // stored PDF always matches the report's current data. The hash is always computed here from
    // the actual uploaded bytes, never trusted from the client.
    public class UploadPathologyReportPdfHandler : IRequestHandler<UploadPathologyReportPdfRequestModel, UploadPathologyReportPdfResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadPathologyReportPdfHandler(
            IConfiguration configuration, IBlobStorageService blobStorageService,
            IWhatsAppMessagingService whatsAppMessagingService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PathologyReportsContainer"] ?? "pathology-reports";
            _blobStorageService = blobStorageService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _context = context;
        }

        public async Task<UploadPathologyReportPdfResponseModel> Handle(UploadPathologyReportPdfRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var report = await _context.PathologyReport
                    .FirstOrDefaultAsync(r => r.ReportId == request.ReportId && r.HospitalId == request.HospitalId, cancellationToken);

                if (report == null)
                {
                    return new UploadPathologyReportPdfResponseModel { Success = false, Message = "Report not found." };
                }

                if (request.File == null || request.File.Length == 0)
                {
                    return new UploadPathologyReportPdfResponseModel { Success = false, Message = "No file uploaded." };
                }

                string sha256Hex;
                using (var stream = request.File.OpenReadStream())
                using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer, cancellationToken);
                    sha256Hex = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
                }

                var url = await _blobStorageService.UploadAsync(request.ReportId.ToString(), request.File, _containerName, cancellationToken);

                report.PdfBlobPath = url;
                report.PdfSha256 = sha256Hex;
                report.UpdatedAt = DateTime.UtcNow;
                _context.PathologyReport.Update(report);
                await _context.SaveChangesAsync(cancellationToken);

                await DispatchWhatsAppLabReportAsync(report.HospitalId, report.OrderId, report.ReportNo, url, cancellationToken);

                return new UploadPathologyReportPdfResponseModel
                {
                    Success = true,
                    Url = url,
                    Sha256 = sha256Hex,
                    Message = "Report PDF uploaded successfully."
                };
            }
            catch (Exception ex)
            {
                return new UploadPathologyReportPdfResponseModel { Success = false, Message = $"An error occurred while uploading the report PDF: {ex.Message}" };
            }
        }

        // Best-effort push -- swallows every failure, matching UploadPrescriptionAttachmentsHandler's
        // dispatch helper. A patient not having a WhatsApp-reachable number on file, WhatsApp being
        // disabled, or the "lab_report_sent" Meta template not being approved yet must never fail
        // the PDF upload itself -- the report is already finalized by this point.
        private async Task DispatchWhatsAppLabReportAsync(
            Guid hospitalId, Guid orderId, string reportNo, string documentLink, CancellationToken cancellationToken)
        {
            try
            {
                var order = await _context.PathologyOrder
                    .Where(o => o.OrderId == orderId && o.HospitalId == hospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (order == null) return;

                var patient = await _context.PatientRegistrations
                    .Where(p => p.PatientId == order.PatientId && p.HospitalId == hospitalId)
                    .Select(p => new { p.Mobile, p.FullName })
                    .FirstOrDefaultAsync(cancellationToken);
                if (patient == null || string.IsNullOrWhiteSpace(patient.Mobile)) return;

                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == hospitalId)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Hospital";

                await _whatsAppMessagingService.SendLabReportAsync(
                    patient.Mobile, documentLink, $"{reportNo}.pdf", hospitalName, patient.FullName ?? "Patient");
            }
            catch (Exception)
            {
                // Swallowed deliberately -- see method summary.
            }
        }
    }
}
