using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UploadPrescriptionDrawingHandler : IRequestHandler<UploadPrescriptionDrawingRequestModel, UploadPrescriptionDrawingResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public UploadPrescriptionDrawingHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionDrawingsContainer"] ?? string.Empty;
        }

        public async Task<UploadPrescriptionDrawingResponseModel> Handle(UploadPrescriptionDrawingRequestModel request, CancellationToken cancellationToken)
        {
            UploadPrescriptionDrawingResponseModel response = new()
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
                    var newDrawingId = Guid.NewGuid();
                    var uploadResult = await _blobStorageService.UploadAsync(newDrawingId.ToString(), request.File, _containerName, cancellationToken);

                    if (!string.IsNullOrEmpty(uploadResult))
                    {
                        // Parse blob name and URL (format: "blobName|presignedUrl").
                        var urlParts = uploadResult.Split('|');
                        var blobName = urlParts.Length > 0 ? urlParts[0] : string.Empty;
                        var fileUrl = urlParts.Length > 1 ? urlParts[1] : uploadResult;

                        var nextSequenceNo = 1 + await _context.PrescriptionDrawings
                            .Where(pd => pd.ApptId == request.AppointmentId)
                            .Select(pd => (int?)pd.SequenceNo)
                            .MaxAsync(cancellationToken) ?? 1;

                        PrescriptionDrawing newDrawing = new()
                        {
                            DrawingId = newDrawingId,
                            ApptId = request.AppointmentId,
                            PatientId = request.PatientId,
                            HospitalId = request.HospitalId,
                            DoctorId = request.DoctorId,
                            Label = request.Label,
                            StorageUrl = fileUrl,
                            FileName = !string.IsNullOrEmpty(blobName) ? blobName : request.FileName,
                            SequenceNo = nextSequenceNo,
                            UploadedAt = DateTime.UtcNow,
                            UploadedBy = request.UserName ?? string.Empty
                        };
                        _context.PrescriptionDrawings.Add(newDrawing);
                        await _context.SaveChangesAsync(cancellationToken);

                        response.Success = true;
                        response.Message = "Drawing successfully uploaded";
                        response.DrawingId = newDrawingId;
                        response.FileUrl = fileUrl;
                        response.SequenceNo = nextSequenceNo;
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
