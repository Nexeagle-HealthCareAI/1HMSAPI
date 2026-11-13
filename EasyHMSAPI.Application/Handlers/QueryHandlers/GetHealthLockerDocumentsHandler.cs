using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHealthLockerDocumentsHandler : IRequestHandler<GetHealthLockerDocumentsRequestModel, GetHealthLockerDocumentsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public GetHealthLockerDocumentsHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<GetHealthLockerDocumentsResponseModel> Handle(GetHealthLockerDocumentsRequestModel request, CancellationToken cancellationToken)
        {
            var documents = await _context.PatientHealthLockerDocuments
                .Where(d => d.Mobile == request.Mobile && (request.All || d.MemberId == request.MemberId))
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new HealthLockerDocumentItem
                {
                    DocumentId = d.DocumentId,
                    ApptId = d.ApptId,
                    MemberId = d.MemberId,
                    VisitDate = d.VisitDate,
                    ExternalDoctorName = d.ExternalDoctorName,
                    ExternalClinicName = d.ExternalClinicName,
                    ExternalLocation = d.ExternalLocation,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    StorageUrl = d.StorageUrl,
                    Notes = d.Notes,
                    UploadedAt = d.UploadedAt,
                })
                .ToListAsync(cancellationToken);

            // For a record tied to an appointment in the system: who and where, and the visit date if they did not give one.
            var apptIds = documents.Where(d => d.ApptId.HasValue).Select(d => d.ApptId!.Value).Distinct().ToList();
            if (apptIds.Count > 0)
            {
                var appts = await _context.Appointments.Where(a => apptIds.Contains(a.ApptId))
                    .Select(a => new { a.ApptId, a.HospitalId, a.DoctorId, a.ApptDate }).ToListAsync(cancellationToken);
                var hospitalIds = appts.Select(a => a.HospitalId).Distinct().ToList();
                var doctorIds = appts.Select(a => a.DoctorId).Distinct().ToList();
                var hospitals = await _context.Hospitals.Where(h => hospitalIds.Contains(h.HospitalID)).ToDictionaryAsync(h => h.HospitalID, h => h.Name, cancellationToken);
                var doctors = await _context.Doctors.Where(x => doctorIds.Contains(x.DoctorID))
                    .Select(x => new { x.DoctorID, Name = x.User.UserProfiles.FirstOrDefault()!.FullName })
                    .ToDictionaryAsync(x => x.DoctorID, x => x.Name, cancellationToken);
                foreach (var doc in documents.Where(d => d.ApptId.HasValue))
                {
                    var a = appts.FirstOrDefault(x => x.ApptId == doc.ApptId);
                    if (a == null) continue;
                    doc.DoctorName = doctors.GetValueOrDefault(a.DoctorId);
                    doc.HospitalName = hospitals.GetValueOrDefault(a.HospitalId);
                    doc.VisitDate ??= a.ApptDate.Date;
                }
            }

            // Re-sign each URL from its stored object key — S3/MinIO presigned URLs expire within
            // 7 days (see docs/STORAGE-MINIO.md). Unlike PrescriptionAttachments, "healthlocker"
            // isn't one of S3StorageService's specially-named containers, so it falls into that
            // service's default "one object per entity" naming: "{entityId}_{containerName}" —
            // NOT the bare "{entityId}_" prefix those special containers use.
            foreach (var doc in documents)
            {
                doc.StorageUrl = await _blobStorageService.RefreshUrlAsync(
                    _containerName,
                    $"{doc.DocumentId}_{_containerName}",
                    doc.StorageUrl,
                    cancellationToken);
            }

            return new GetHealthLockerDocumentsResponseModel
            {
                Success = true,
                Message = documents.Count == 0 ? "No documents in your health locker yet." : "Documents retrieved successfully.",
                Documents = documents,
            };
        }
    }
}
