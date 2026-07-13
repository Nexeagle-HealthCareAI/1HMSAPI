using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public (Nexeagle-facing) doctor listing — hospital scoped from the caller's API key, never
    /// from a client-supplied id. Deliberately narrower than GetDepartmentDoctorsHandler /
    /// GetHospitalDoctorsHandler: excludes LicenseNumber, MedicalCouncil, RegistrationYear, UserId,
    /// and any mobile/email/queue-internal field, and additionally resolves a fresh presigned photo
    /// URL per doctor (same GetUrlAsync pattern as GetProfilePictureHandler — presigned URLs expire,
    /// so this is never cached long-term).
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
            if (request.HospitalId == Guid.Empty)
                return new GetPublicDoctorsResponseModel { Success = false, Message = "HospitalId is required." };

            var rows = await (
                from d in _context.Doctors
                join u in _context.Users on d.UserID equals u.UserID
                where d.HospitalId == request.HospitalId && u.UserStatusId != (int)UserStatusEnum.Revoked
                select new { d.DoctorID, d.UserID, d.PrimaryDepartmentID, d.Qualification, d.ExperienceYears, d.Bio }
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

            var doctors = new List<PublicDoctorInfo>();
            foreach (var r in rows)
            {
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

                var photoUrl = await _blobService.GetUrlAsync(r.UserID, _containerName, cancellationToken) as string;

                doctors.Add(new PublicDoctorInfo
                {
                    DoctorId = r.DoctorID,
                    FullName = nameLookup.TryGetValue(r.UserID, out var n) ? n : null,
                    PhotoUrl = photoUrl,
                    Qualification = r.Qualification,
                    ExperienceYears = r.ExperienceYears,
                    Bio = r.Bio,
                    DepartmentName = r.PrimaryDepartmentID.HasValue && deptNameById.TryGetValue(r.PrimaryDepartmentID.Value, out var dn) ? dn : null,
                    Specializations = specializations,
                });
            }

            return new GetPublicDoctorsResponseModel { Success = true, Doctors = doctors.OrderBy(d => d.FullName).ToList() };
        }
    }
}
