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
        private readonly string _containerName;

        public UploadPrescriptionAttachmentsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
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
                        AppConstants.AppointmentStatus_AwaitingReconsult,
                        AppConstants.AppointmentStatus_Completed,
                        AppConstants.AppointmentStatus_LabRequired
                    };

                    if (!string.IsNullOrEmpty(appointment?.CurrentStatusCode) && allowedStatuses.Contains(appointment.CurrentStatusCode.ToUpper()))
                    {
                        var newAttachmentId = Guid.NewGuid();
                        var fileUrl = await _blobStorageService.UploadAsync(newAttachmentId.ToString(), request.File, _containerName, cancellationToken);

                        if (!string.IsNullOrEmpty(fileUrl))
                        {
                            PrescriptionAttachment newAttachment = new()
                            {
                                AttachmentId = newAttachmentId,
                                ApptId = request.AppointmentId,
                                PatientId = request.PatientId,
                                HospitalId = request.HospitalId,
                                DoctorId = request.DoctorId,
                                ReportType = request.ReportType,
                                StorageUrl = fileUrl,
                                FileName = request.FileName,
                                Notes = request.Notes,
                                UploadedAt = DateTime.UtcNow,
                                UploadedBy = request.UserName ?? string.Empty
                            };
                            _context.PrescriptionAttachments.Add(newAttachment);

                            if(appointment.CurrentStatusCode.ToUpper() == AppConstants.AppointmentStatus_LabRequired)
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
                        }
                    }
                    else
                    {
                        response.Message = "This appointment is not allowed for attchment uploads";
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
    }
}
