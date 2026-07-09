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
    public class GetPrescriptionDrawingsHandler : IRequestHandler<GetPrescriptionDrawingsRequestModel, GetPrescriptionDrawingsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _drawingsContainer;

        public GetPrescriptionDrawingsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _drawingsContainer = configuration["BlobStorage:PrescriptionDrawingsContainer"] ?? string.Empty;
        }

        public async Task<GetPrescriptionDrawingsResponseModel> Handle(GetPrescriptionDrawingsRequestModel request, CancellationToken cancellationToken)
        {
            GetPrescriptionDrawingsResponseModel response = new()
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

                var drawings = await _context.PrescriptionDrawings
                    .Where(pd => pd.ApptId == request.AppointmentId &&
                                 pd.PatientId == request.PatientId &&
                                 pd.HospitalId == request.HospitalId &&
                                 pd.DoctorId == request.DoctorId)
                    .OrderBy(pd => pd.SequenceNo)
                    .Select(pd => new PrescriptionDrawingDataModel
                    {
                        DrawingId = pd.DrawingId,
                        Label = pd.Label,
                        FileName = pd.FileName,
                        StorageUrl = pd.StorageUrl,
                        SequenceNo = pd.SequenceNo,
                        UploadedAt = pd.UploadedAt,
                        UploadedBy = pd.UploadedBy
                    })
                    .ToListAsync(cancellationToken);
                if (drawings == null || drawings.Count == 0)
                {
                    response.Message = "No drawings found for the specified criteria.";
                    response.Success = true;
                    response.DrawingCount = 0;
                    response.Drawings = new List<PrescriptionDrawingDataModel>();
                }
                else
                {
                    // Re-sign each drawing URL from its stored object key so links never go stale
                    // (S3/MinIO presigned URLs expire within 7 days).
                    foreach (var drawing in drawings)
                    {
                        drawing.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                            _drawingsContainer,
                            $"{drawing.DrawingId}_",
                            drawing.StorageUrl,
                            cancellationToken);
                    }

                    response.DrawingCount = drawings.Count;
                    response.Drawings = drawings;
                    response.Success = true;
                    response.Message = "Drawings retrieved successfully.";
                }
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
