using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Rich, hospital-scoped doctor list for the admin Public Directory tile editor. Unlike
    /// GetPublicDoctorsHandler (platform-wide, public-facing, only publicly-listed doctors), this
    /// returns every doctor at the requesting hospital regardless of listing status, since an admin
    /// needs to edit/toggle a doctor before they're publicly listed. Unlike GetHospitalDoctorsHandler
    /// (the lean admit-form picker), this resolves photo URLs and specializations per doctor —
    /// deliberately kept out of that hot, simple-dropdown endpoint.
    /// </summary>
    public class GetPublicDirectoryDoctorsHandler : IRequestHandler<GetPublicDirectoryDoctorsRequestModel, GetPublicDirectoryDoctorsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobService;
        private readonly string _containerName;

        public GetPublicDirectoryDoctorsHandler(AppDbContext context, IBlobStorageService blobService, IConfiguration configuration)
        {
            _context = context;
            _blobService = blobService;
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
        }

        public async Task<GetPublicDirectoryDoctorsResponseModel> Handle(GetPublicDirectoryDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new GetPublicDirectoryDoctorsResponseModel { Success = false, Message = "HospitalId is required." };

            var doctorIds = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId == request.HospitalId)
                .Select(dd => dd.DoctorID)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (doctorIds.Count == 0)
                return new GetPublicDirectoryDoctorsResponseModel { Success = true, Doctors = new() };

            var rows = await (
                from d in _context.Doctors
                where doctorIds.Contains(d.DoctorID)
                select new
                {
                    d.DoctorID,
                    d.UserID,
                    d.PrimaryDepartmentID,
                    d.LicenseNumber,
                    d.MedicalCouncil,
                    d.RegistrationYear,
                    d.Qualification,
                    d.ExperienceYears,
                    d.Bio,
                    d.LanguagesJson,
                    d.PublicContactEmail,
                    d.PublicContactPhone,
                    d.IsPubliclyListed,
                    d.IsFeatured,
                    d.IsDelistedByAdmin,
                    d.IsRegistrationVerified,
                    d.DiscountPercent,
                    d.DiscountStartAt,
                    d.DiscountEndAt,
                }
            ).ToListAsync(cancellationToken);

            var userIds = rows.Select(r => r.UserID).Distinct().ToList();
            var nameByUser = await _context.UserProfiles
                .Where(up => userIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .Select(up => new { up.UserID, up.FullName })
                .ToListAsync(cancellationToken);
            var nameLookup = nameByUser
                .GroupBy(n => n.UserID)
                .ToDictionary(g => g.Key, g => g.First().FullName);

            var deptIds = rows.Where(r => r.PrimaryDepartmentID.HasValue).Select(r => r.PrimaryDepartmentID!.Value).Distinct().ToList();
            var deptNameById = await _context.Departments
                .Where(dept => deptIds.Contains(dept.DepartmentID))
                .ToDictionaryAsync(dept => dept.DepartmentID, dept => dept.Name, cancellationToken);

            // Resolves the mapping PUT doctors/profile's existing HospitalDepartmentMappingId guard
            // requires — same lookup DoctorGetHandler already does per-department, narrowed here to
            // this hospital since the tile editor only ever writes back for the requesting hospital.
            var mappingByDept = await _context.HospitalDepartmentMappings
                .Where(hdm => hdm.HospitalID == request.HospitalId && deptIds.Contains(hdm.DepartmentID))
                .ToDictionaryAsync(hdm => hdm.DepartmentID, hdm => hdm.MappingID, cancellationToken);

            // Fetch presigned photo URLs concurrently rather than one await per doctor.
            var photoUrls = await Task.WhenAll(
                rows.Select(r => _blobService.GetUrlAsync(r.UserID, _containerName, cancellationToken))
            );

            var doctorIdsForReviews = rows.Select(r => r.DoctorID).ToList();
            var reviewAggregates = await _context.DoctorReviews
                .Where(r => doctorIdsForReviews.Contains(r.DoctorId) && !r.IsHidden && !r.IsHospitalResponse)
                .GroupBy(r => r.DoctorId)
                .Select(g => new { DoctorId = g.Key, Average = g.Average(r => r.Rating), Count = g.Count() })
                .ToDictionaryAsync(g => g.DoctorId, cancellationToken);

            // Same dbo.DoctorFees rows Configuration > Doctor Fees edits — grouped per doctor so the
            // tile editor can show/edit OPD fee and round-trip IPD/Emergency unchanged on save.
            var feesByDoctor = await _context.DoctorFees
                .Where(f => f.HospitalId == request.HospitalId && doctorIdsForReviews.Contains(f.DoctorId) && f.IsActive
                    && (f.FeeType == "OPD_CONSULT" || f.FeeType == "IPD_VISIT" || f.FeeType == "EMERGENCY"))
                .ToListAsync(cancellationToken);
            var feesByDoctorLookup = feesByDoctor.GroupBy(f => f.DoctorId).ToDictionary(g => g.Key, g => g.ToList());

            var doctors = new List<PublicDirectoryDoctorItem>();
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                reviewAggregates.TryGetValue(r.DoctorID, out var reviewAgg);
                feesByDoctorLookup.TryGetValue(r.DoctorID, out var doctorFees);
                var opdFee = doctorFees?.FirstOrDefault(f => f.FeeType == "OPD_CONSULT")?.Amount;
                var ipdFee = doctorFees?.FirstOrDefault(f => f.FeeType == "IPD_VISIT")?.Amount;
                var emergencyFee = doctorFees?.FirstOrDefault(f => f.FeeType == "EMERGENCY")?.Amount;
                var specializations = await _context.DoctorSpecializations
                    .Where(ds => ds.DoctorID == r.DoctorID && ds.Specialization != null && ds.Specialization.IsActive)
                    .Select(ds => ds.Specialization.Name)
                    .ToListAsync(cancellationToken);

                doctors.Add(new PublicDirectoryDoctorItem
                {
                    DoctorId = r.DoctorID,
                    UserId = r.UserID,
                    FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                    PhotoUrl = photoUrls[i] as string,
                    DepartmentId = r.PrimaryDepartmentID,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    HospitalDepartmentMappingId = r.PrimaryDepartmentID.HasValue && mappingByDept.TryGetValue(r.PrimaryDepartmentID.Value, out var mid) ? mid : (Guid?)null,
                    LicenseNumber = r.LicenseNumber,
                    MedicalCouncil = r.MedicalCouncil,
                    RegistrationYear = r.RegistrationYear,
                    Qualification = r.Qualification,
                    ExperienceYears = r.ExperienceYears,
                    Bio = r.Bio,
                    OpdConsultFee = opdFee,
                    IpdVisitFee = ipdFee,
                    EmergencyFee = emergencyFee,
                    Specializations = specializations
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Languages = string.IsNullOrWhiteSpace(r.LanguagesJson)
                        ? new List<string>()
                        : (JsonSerializer.Deserialize<List<string>>(r.LanguagesJson) ?? new List<string>()),
                    PublicContactEmail = r.PublicContactEmail,
                    PublicContactPhone = r.PublicContactPhone,
                    IsPubliclyListed = r.IsPubliclyListed,
                    IsFeatured = r.IsFeatured,
                    IsDelistedByAdmin = r.IsDelistedByAdmin,
                    IsRegistrationVerified = r.IsRegistrationVerified,
                    DiscountPercent = r.DiscountPercent,
                    DiscountStartAt = r.DiscountStartAt,
                    DiscountEndAt = r.DiscountEndAt,
                    Rating = reviewAgg != null ? Math.Round(reviewAgg.Average, 1) : (double?)null,
                    ReviewCount = reviewAgg?.Count ?? 0,
                });
            }

            return new GetPublicDirectoryDoctorsResponseModel { Success = true, Doctors = doctors.OrderBy(d => d.FullName).ToList() };
        }
    }
}
