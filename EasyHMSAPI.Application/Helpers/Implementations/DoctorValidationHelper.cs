using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Helpers.Implementations
{
    public class DoctorValidationHelper : IDoctorValidationHelper
    {
        private readonly AppDbContext _dbContext;
        public DoctorValidationHelper(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<bool> ValidateDoctorAsync(Guid hospitalId, Guid doctorId, CancellationToken cancellationToken)
        {
            var doctorUserId = await _dbContext.Doctors
                .Where(d => d.DoctorID == doctorId)
                .Select(d => new { d.UserID })
                .FirstOrDefaultAsync(cancellationToken);
            if (doctorUserId == null) return false;

            var isLinkedWithHospital = await _dbContext.HospitalUsers
                .AnyAsync(hu => hu.HospitalID == hospitalId && hu.UserID == doctorUserId.UserID, cancellationToken);

            return isLinkedWithHospital;
        }
    }
}
