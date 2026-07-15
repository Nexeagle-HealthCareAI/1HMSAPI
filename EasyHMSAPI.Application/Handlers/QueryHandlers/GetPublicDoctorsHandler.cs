using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) doctor listing — platform-wide, spans every hospital that has
    /// opted into the public directory (Hospital.IsPubliclyListed), not scoped to one hospital.
    /// A doctor only appears when BOTH their hospital and the doctor themself (Doctor.IsPubliclyListed)
    /// have opted in — hospital-scope resolved via DoctorDepartments, not the single retrofitted
    /// Doctor.HospitalId field (see GetDoctorFeesHandler for the same convention).
    /// Deliberately narrower than GetDepartmentDoctorsHandler / GetHospitalDoctorsHandler:
    /// excludes LicenseNumber, MedicalCouncil, RegistrationYear, UserId, and any mobile/email/
    /// queue-internal field, and additionally resolves a fresh presigned photo URL per doctor
    /// (same GetUrlAsync pattern as GetProfilePictureHandler — presigned URLs expire, so this is
    /// never cached long-term).
    /// </summary>
    public class GetPublicDoctorsHandler : IRequestHandler<GetPublicDoctorsRequestModel, GetPublicDoctorsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobService;
        private readonly string _containerName;

        public GetPublicDoctorsHandler(AppDbContext context, IBlobStorageService blobService, IConfiguration configuration)
        {
            _context = context;
            _blobService = blobService;
            _containerName = configuration["BlobStorage:ProfilePhotosContainer"] ?? string.Empty;
        }

        public async Task<GetPublicDoctorsResponseModel> Handle(GetPublicDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            var publicHospitalIds = await _context.Hospitals
                .Where(h => h.IsPubliclyListed && h.IsActive)
                .Select(h => h.HospitalID)
                .ToListAsync(cancellationToken);

            if (publicHospitalIds.Count == 0)
                return new GetPublicDoctorsResponseModel { Success = true, Doctors = new() };

            // Doctor -> hospital affiliations restricted to publicly-listed hospitals. Doctor.HospitalId
            // is a single retrofitted field, not the source of truth (see GetDoctorFeesHandler) — a
            // doctor is a global identity that can have DoctorDepartments rows at more than one
            // hospital. True multi-hospital doctor practice isn't a live product scenario yet, so pick
            // one hospital deterministically per doctor rather than fanning out to multiple rows.
            var doctorHospitalPairs = await _context.DoctorDepartments
                .Where(dd => dd.HospitalId.HasValue && publicHospitalIds.Contains(dd.HospitalId!.Value))
                .Select(dd => new { dd.DoctorID, HospitalId = dd.HospitalId!.Value })
                .Distinct()
                .ToListAsync(cancellationToken);

            var doctorHospital = doctorHospitalPairs
                .GroupBy(p => p.DoctorID)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.HospitalId).First().HospitalId);

            if (doctorHospital.Count == 0)
                return new GetPublicDoctorsResponseModel { Success = true, Doctors = new() };

            var candidateDoctorIds = doctorHospital.Keys.ToList();

            var rows = await (
                from d in _context.Doctors
                where candidateDoctorIds.Contains(d.DoctorID) && d.IsPubliclyListed
                join u in _context.Users on d.UserID equals u.UserID
                where u.UserStatusId != (int)UserStatusEnum.Revoked
                select new { d.DoctorID, d.UserID, d.PrimaryDepartmentID, d.Qualification, d.ExperienceYears, d.Bio, d.LanguagesJson }
            ).ToListAsync(cancellationToken);

            var hospitalIdsUsed = rows.Select(r => doctorHospital[r.DoctorID]).Distinct().ToList();
            var hospitalById = await _context.Hospitals
                .Where(h => hospitalIdsUsed.Contains(h.HospitalID))
                .Select(h => new { h.HospitalID, h.Name, h.City, h.State, h.Latitude, h.Longitude })
                .ToDictionaryAsync(h => h.HospitalID, cancellationToken);

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

            // One batched aggregate query for the whole platform-wide listing rather than a
            // per-doctor round trip.
            var reviewAggregates = await _context.DoctorReviews
                .Where(r => candidateDoctorIds.Contains(r.DoctorId) && !r.IsHidden && !r.IsHospitalResponse)
                .GroupBy(r => r.DoctorId)
                .Select(g => new { DoctorId = g.Key, Average = g.Average(r => r.Rating), Count = g.Count() })
                .ToDictionaryAsync(g => g.DoctorId, cancellationToken);

            // Photo-URL resolution now scales with platform-wide doctor count rather than one
            // hospital's — fetch presigned URLs concurrently instead of one await per doctor.
            var photoUrls = await Task.WhenAll(
                rows.Select(r => _blobService.GetUrlAsync(r.UserID, _containerName, cancellationToken))
            );

            var doctors = new List<PublicDoctorInfo>();
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var hospitalId = doctorHospital[r.DoctorID];
                hospitalById.TryGetValue(hospitalId, out var hospital);
                reviewAggregates.TryGetValue(r.DoctorID, out var reviewAgg);
                var specializations = _context.DoctorSpecializations
                    .Where(ds => ds.DoctorID == r.DoctorID && ds.Specialization != null && ds.Specialization.IsActive)
                    .Select(ds => ds.Specialization.Name)
                    .AsEnumerable()
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                                   name.Trim().Length > 2 &&
                                   name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c)))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                doctors.Add(new PublicDoctorInfo
                {
                    DoctorId = r.DoctorID,
                    FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                    PhotoUrl = photoUrls[i] as string,
                    Qualification = r.Qualification,
                    ExperienceYears = r.ExperienceYears,
                    Bio = r.Bio,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    Specializations = specializations,
                    Languages = string.IsNullOrWhiteSpace(r.LanguagesJson)
                        ? new List<string>()
                        : (JsonSerializer.Deserialize<List<string>>(r.LanguagesJson) ?? new List<string>()),
                    HospitalId = hospitalId,
                    HospitalName = hospital?.Name,
                    City = hospital?.City,
                    State = hospital?.State,
                    Latitude = hospital?.Latitude,
                    Longitude = hospital?.Longitude,
                    Rating = reviewAgg != null ? Math.Round(reviewAgg.Average, 1) : (double?)null,
                    ReviewCount = reviewAgg?.Count ?? 0,
                });
            }

            return new GetPublicDoctorsResponseModel { Success = true, Doctors = doctors.OrderBy(d => d.FullName).ToList() };
        }
    }
}
