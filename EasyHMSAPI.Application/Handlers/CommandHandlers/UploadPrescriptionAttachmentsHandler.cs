using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadPrescriptionAttachmentsHandler : IRequestHandler<UploadPrescriptionAttachmentsRequestModel, UploadPrescriptionAttachmentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _containerName;

        public UploadPrescriptionAttachmentsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _containerName = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }
        public async Task<UploadPrescriptionAttachmentsResponseModel> Handle(UploadPrescriptionAttachmentsRequestModel request, CancellationToken cancellationToken)
        {
            UploadPrescriptionAttachmentsResponseModel response = new()
            {
                Success = false,
            };
            try
            {
                var existingDoctor = await _context.Doctors
                  .Where(x => x.DoctorID == request.DoctorId)
                  .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                    return response;
                }

                var existingHospital = await _context.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital == null)
                {
                    response.Message = "Hospital not found.";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var appointment = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId && x.PatientId == request.PatientId && x.DoctorId == request.DoctorId && x.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (appointment is not null)
                {
                    List<string> allowedStatuses = new()
                    {
                        AppConstants.AppointmentStatus_Ready,
                        AppConstants.AppointmentStatus_UnderConsult,
                        AppConstants.AppointmentStatus_AwaitingReconsult,
                        AppConstants.AppointmentStatus_Completed,
                        AppConstants.AppointmentStatus_LabRequired
                    };

                    if (!string.IsNullOrEmpty(appointment.CurrentStatusCode) && allowedStatuses.Contains(appointment.CurrentStatusCode.ToUpper()))
                    {
                        var targetContainer = request.ReportType?.Equals("Lab Report", StringComparison.OrdinalIgnoreCase) == true
                            ? "labreports"
                            : _containerName;

                        var newAttachmentId = request.AttachmentId ?? Guid.NewGuid();
                        var uploadResult = await _blobStorageService.UploadAsync(newAttachmentId.ToString(), request.File, targetContainer, cancellationToken);

                        if (!string.IsNullOrEmpty(uploadResult))
                        {
                            // Parse blob name and URL (format: "blobName|sasUrl")
                            var urlParts = uploadResult.Split('|');
                            var blobName = urlParts.Length > 0 ? urlParts[0] : string.Empty;
                            var fileUrl = urlParts.Length > 1 ? urlParts[1] : uploadResult;

                            PrescriptionAttachment newAttachment = new()
                            {
                                AttachmentId = newAttachmentId,
                                ApptId = request.AppointmentId,
                                PatientId = request.PatientId,
                                HospitalId = request.HospitalId,
                                DoctorId = request.DoctorId,
                                ReportType = request.ReportType,
                                StorageUrl = fileUrl,
                                FileName = !string.IsNullOrEmpty(blobName) ? blobName : request.FileName,
                                Notes = request.Notes,
                                UploadedAt = DateTime.UtcNow,
                                UploadedBy = request.UserName ?? string.Empty
                            };
                            _context.PrescriptionAttachments.Add(newAttachment);

                            if (appointment.CurrentStatusCode.ToUpper() == AppConstants.AppointmentStatus_LabRequired)
                            {
                                appointment.CurrentStatusCode = AppConstants.AppointmentStatus_AwaitingReconsult;
                                var history = string.IsNullOrEmpty(appointment.StatusHistoryJson)
                                    ? new List<object>()
                                    : JsonSerializer.Deserialize<List<object>>(appointment.StatusHistoryJson) ?? new List<object>();
                                history.Add(new { status = AppConstants.AppointmentStatus_AwaitingReconsult, timestamp = DateTime.Now });
                                appointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                appointment.LastStatusCodeAt = DateTime.UtcNow;
                            }
                            await _context.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Attachment successfully uploaded";
                            response.AttachmentId = newAttachmentId;
                            response.FileUrl = fileUrl;

                            // Best-effort: a patient with a WhatsApp-reachable number gets this
                            // automatically, whether the upload came from InkRx's auto-save or a
                            // manual portal upload -- both land here, it's the same handler. Never
                            // lets a WhatsApp failure affect the upload's own success response.
                            if (string.Equals(request.ReportType, "Prescription", StringComparison.OrdinalIgnoreCase))
                            {
                                await TrySendPrescriptionWhatsAppAsync(
                                    request.PatientId!, request.HospitalId, existingHospital.Name, existingDoctor.DoctorID,
                                    fileUrl, !string.IsNullOrEmpty(blobName) ? blobName : (request.FileName ?? "Prescription.pdf"), cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        response.Message = "This appointment is not allowed for attachment uploads.";
                    }
                }
                else
                {
                    response.Message = "Appointment not found for the given patient.";
                }
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
                return response;
            }

            return response;
        }

        // Best-effort push -- swallows every failure. A patient not having a WhatsApp-reachable
        // number on file, WhatsApp being disabled, or the Meta template not being approved yet
        // are all expected, non-error outcomes here, not something the upload caller needs to
        // know about.
        private async Task TrySendPrescriptionWhatsAppAsync(
            string patientId, Guid hospitalId, string hospitalName, Guid doctorId,
            string documentLink, string fileName, CancellationToken cancellationToken)
        {
            try
            {
                var mobile = await _context.PatientRegistrations
                    .Where(p => p.PatientId == patientId && p.HospitalId == hospitalId)
                    .Select(p => p.Mobile)
                    .FirstOrDefaultAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(mobile))
                    return;

                var doctor = await _context.Doctors
                    .Where(d => d.DoctorID == doctorId)
                    .Include(d => d.User)
                    .ThenInclude(u => u.UserProfiles)
                    .FirstOrDefaultAsync(cancellationToken);
                var doctorName = doctor?.User?.UserProfiles?.FirstOrDefault()?.FullName ?? "Doctor";

                await _whatsAppMessagingService.SendPrescriptionAsync(mobile, documentLink, fileName, hospitalName, doctorName);
            }
            catch (Exception)
            {
                // Swallowed deliberately -- see method summary.
            }
        }
    }
}
