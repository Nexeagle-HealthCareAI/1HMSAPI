using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadVisitSummaryHandler : IRequestHandler<UploadVisitSummaryRequestModel, UploadVisitSummaryResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly string _containerName;

        public UploadVisitSummaryHandler(AppDbContext context, IBlobStorageService blobStorageService, IWhatsAppMessagingService whatsAppMessagingService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _containerName = configuration["BlobStorage:PrescriptionsContainer"] ?? string.Empty;
        }

        public async Task<UploadVisitSummaryResponseModel> Handle(UploadVisitSummaryRequestModel request, CancellationToken cancellationToken)
        {
            UploadVisitSummaryResponseModel response = new();
            try
            {
                var existingAppointment  = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingAppointment is not null)
                {
                    string fileIdentifier = $"Appt_{existingAppointment.ApptId}_Doc_{existingAppointment.DoctorId}_Pat_{existingAppointment.PatientId}";
                    var fileUrl = await _blobStorageService.UploadAsync(fileIdentifier, request.File, _containerName, cancellationToken);
                    if(!string.IsNullOrEmpty(fileUrl))
                    {
                        existingAppointment.PdfUrl = fileUrl;
                        await _context.SaveChangesAsync(cancellationToken);
                        
                        // Fetch patient details for WhatsApp notification
                        var patientMobileNumber = await _context.PatientRegistrations
                            .Where(p => p.PatientId == existingAppointment.PatientId)
                            .Select(x => x.Mobile)
                            .FirstOrDefaultAsync(cancellationToken);

                        // Fetch hospital details
                        var hospitalName = await _context.Hospitals
                            .Where(h => h.HospitalID == existingAppointment.HospitalId)
                            .Select(h => h.Name)
                            .FirstOrDefaultAsync(cancellationToken);

                        // Fetch doctor details
                        var doctor = await _context.Doctors
                            .Where(d => d.DoctorID == existingAppointment.DoctorId)
                            .Include(d => d.User)
                            .ThenInclude(u => u.UserProfiles)
                            .FirstOrDefaultAsync(cancellationToken);

                        var filename = $"Prescription_{existingAppointment.ApptId}.pdf";
                        var doctorName = string.Empty;

                        if (doctor is not null)
                        {
                            doctorName = doctor.User?.UserProfiles?.FirstOrDefault()?.FullName ?? "Doctor";
                        }

                        await _whatsAppMessagingService.SendPrescriptionAsync(
                            patientMobileNumber ?? string.Empty,
                            fileUrl,
                            filename,
                            hospitalName ?? string.Empty,
                            doctorName);

                        response.Success = true;
                        response.Message = "Visit summary uploaded successfully.";
                        response.Url = fileUrl;
                        response.IsSentViaWhatsApp = true;
                    }
                    else
                    {
                        response.Success = false;
                        response.IsSentViaWhatsApp = false;
                        response.Message = "Failed to upload visit summary.";
                    }
                }
                else
                {
                    response.Success = false;
                    response.IsSentViaWhatsApp = false;
                    response.Message = "Appointment not found.";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.IsSentViaWhatsApp = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
