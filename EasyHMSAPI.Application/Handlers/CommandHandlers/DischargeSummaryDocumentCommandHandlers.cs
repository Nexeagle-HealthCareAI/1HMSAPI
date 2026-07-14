using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Uploads the discharge summary PDF to blob storage (for the QR "view anytime" link and
    /// WhatsApp send) and sends it via WhatsApp. PdfBlobKey is a stable object-key prefix, never a
    /// raw URL -- S3 presigned URLs expire, so every read (public view or WhatsApp send) re-signs a
    /// fresh one from this key via IBlobStorageService.RefreshUrlAsync.
    /// </summary>
    public class DischargeSummaryDocumentCommandHandlers :
        IRequestHandler<UploadDischargeSummaryPdfRequestModel, UploadDischargeSummaryPdfResponseModel>,
        IRequestHandler<SendDischargeSummaryWhatsAppRequestModel, SendDischargeSummaryWhatsAppResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _containerName;

        public DischargeSummaryDocumentCommandHandlers(AppDbContext context, IBlobStorageService blobStorageService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _containerName = configuration["BlobStorage:DischargeSummaryContainer"] ?? string.Empty;
        }

        public async Task<UploadDischargeSummaryPdfResponseModel> Handle(UploadDischargeSummaryPdfRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new UploadDischargeSummaryPdfResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (request.File == null || request.File.Length == 0)
                    return new UploadDischargeSummaryPdfResponseModel { Success = false, Message = "A PDF file is required." };

                var summary = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);
                if (summary == null)
                    return new UploadDischargeSummaryPdfResponseModel { Success = false, Message = "Save the discharge summary before generating a shareable PDF." };

                var entityId = summary.DischargeSummaryId.ToString();
                await _blobStorageService.UploadAsync(entityId, request.File, _containerName, cancellationToken);

                var now = DateTime.UtcNow;
                summary.PdfBlobKey = $"{entityId}_{_containerName}";
                summary.PdfUploadedAt = now;
                if (string.IsNullOrEmpty(summary.AccessToken))
                    summary.AccessToken = RandomNumberGenerator.GetHexString(40);
                summary.UpdatedAt = now;

                await _context.SaveChangesAsync(cancellationToken);

                return new UploadDischargeSummaryPdfResponseModel { Success = true, Message = "Discharge summary PDF uploaded.", AccessToken = summary.AccessToken };
            }
            catch (Exception)
            {
                return new UploadDischargeSummaryPdfResponseModel { Success = false, Message = "Error uploading discharge summary PDF." };
            }
        }

        public async Task<SendDischargeSummaryWhatsAppResponseModel> Handle(SendDischargeSummaryWhatsAppRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var summary = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);
                if (summary == null || string.IsNullOrEmpty(summary.PdfBlobKey))
                    return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "Generate the discharge summary PDF before sending it." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "Admission not found." };

                var mobile = !string.IsNullOrWhiteSpace(request.MobileNumber)
                    ? request.MobileNumber
                    : await _context.PatientRegistrations
                        .Where(p => p.PatientId == admission.PatientId)
                        .Select(p => p.Mobile)
                        .FirstOrDefaultAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(mobile))
                    return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "No mobile number on file for this patient." };

                var freshUrl = await _blobStorageService.RefreshUrlAsync(_containerName, summary.PdfBlobKey, null, cancellationToken);
                if (string.IsNullOrEmpty(freshUrl))
                    return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "The document could not be found." };

                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == request.HospitalId)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                string? doctorName = null;
                if (admission.PrimaryDoctorId.HasValue)
                {
                    var doctor = await _context.Doctors
                        .Where(d => d.DoctorID == admission.PrimaryDoctorId.Value)
                        .Include(d => d.User)
                        .ThenInclude(u => u.UserProfiles)
                        .FirstOrDefaultAsync(cancellationToken);
                    doctorName = doctor?.User?.UserProfiles?.FirstOrDefault()?.FullName;
                }

                var sent = await _whatsAppMessagingService.SendDischargeSummaryAsync(
                    mobile!, freshUrl, $"DischargeSummary_{admission.AdmissionNo}.pdf", hospitalName ?? string.Empty, doctorName ?? "Doctor");

                return sent
                    ? new SendDischargeSummaryWhatsAppResponseModel { Success = true, Message = "Discharge summary sent via WhatsApp." }
                    : new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "Could not send via WhatsApp. WhatsApp may be disabled, or the message template isn't approved yet." };
            }
            catch (Exception)
            {
                return new SendDischargeSummaryWhatsAppResponseModel { Success = false, Message = "Error sending discharge summary via WhatsApp." };
            }
        }
    }
}
