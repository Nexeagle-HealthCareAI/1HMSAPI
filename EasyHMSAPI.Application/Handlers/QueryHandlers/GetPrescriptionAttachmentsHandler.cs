using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPrescriptionAttachmentsHandler : IRequestHandler<GetPrescriptionAttachmentsRequestModel, GetPrescriptionAttachmentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _attachmentsContainer;

        public GetPrescriptionAttachmentsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _attachmentsContainer = configuration["BlobStorage:PrescriptionAttachmentsContainer"] ?? string.Empty;
        }

        public async Task<GetPrescriptionAttachmentsResponseModel> Handle(GetPrescriptionAttachmentsRequestModel request, CancellationToken cancellationToken)
        {
            GetPrescriptionAttachmentsResponseModel response = new()
            {
                AppointmentId = request.AppointmentId,
                PatientId = request.PatientId,
                HospitalId = request.HospitalId,
                DoctorId = request.DoctorId,
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

                var attachments = await _context.PrescriptionAttachments
                    .Where(pa => pa.ApptId == request.AppointmentId &&
                                 pa.PatientId == request.PatientId &&
                                 pa.HospitalId == request.HospitalId &&
                                 pa.DoctorId == request.DoctorId)
                    .Select(pa => new AttachmentsDataModel
                    {
                        AttachmentId = pa.AttachmentId,
                        ReportType = pa.ReportType,
                        FileName = pa.FileName,
                        StorageUrl = pa.StorageUrl,
                        Notes = pa.Notes,
                        UploadedAt = pa.UploadedAt,
                        UploadedBy = pa.UploadedBy
                    })
                    .ToListAsync(cancellationToken);
                if(attachments == null || attachments.Count == 0)
                {
                    response.Message = "No attachments found for the specified criteria.";
                }
                else
                {
                    // Re-sign each attachment URL from its stored object key so links never go stale
                    // (S3/MinIO presigned URLs expire within 7 days; Azure returns them unchanged).
                    foreach (var attachment in attachments)
                    {
                        attachment.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                            _attachmentsContainer,
                            $"{attachment.AttachmentId}_",
                            attachment.StorageUrl,
                            cancellationToken);
                    }

                    response.AttachmentCount = attachments.Count;
                    response.Attachments = attachments;
                    response.Success = true;
                    response.Message = "Attachments retrieved successfully.";
                }
            }
            catch (Exception ex)
            {
                response.Message = "Error Occureed" + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
