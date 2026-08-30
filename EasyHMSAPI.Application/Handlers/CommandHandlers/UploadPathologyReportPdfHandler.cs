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
    // The technician/pathologist signature blocks and the QR verification code are only meaningful
    // once burned into a real PDF, so this is deliberately the LAST step of the sign-off flow --
    // called by the frontend right after ApprovePathologyReportHandler succeeds, once it has
    // rendered the final document client-side (see generatePathologyReportPdf.ts). The hash is
    // always computed here from the actual uploaded bytes, never trusted from the client, so the
    // public verification endpoint's tamper check means something.
    public class UploadPathologyReportPdfHandler : IRequestHandler<UploadPathologyReportPdfRequestModel, UploadPathologyReportPdfResponseModel>
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;
        private readonly AppDbContext _context;

        public UploadPathologyReportPdfHandler(IConfiguration configuration, IBlobStorageService blobStorageService, AppDbContext context)
        {
            _containerName = configuration["BlobStorage:PathologyReportsContainer"] ?? "pathology-reports";
            _blobStorageService = blobStorageService;
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

                if (report.Status != "APPROVED")
                {
                    return new UploadPathologyReportPdfResponseModel { Success = false, Message = "The final PDF can only be uploaded after pathologist approval." };
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
    }
}
