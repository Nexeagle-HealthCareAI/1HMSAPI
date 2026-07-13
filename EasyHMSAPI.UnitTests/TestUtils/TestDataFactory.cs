using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace EasyHMSAPI.UnitTests.TestUtils
{
    public static class TestDataFactory
    {
        public static User SeedUser(AppDbContext context, string email = "test@example.com", string phone = "1234567890", string password = "password", string role = "Patient", bool isActive = true)
        {
            var user = new User
            {
                UserID = Guid.NewGuid(),
                Email = email,
                MobileNumber = phone,
                UserStatusId = isActive ? (int)UserStatusEnum.Active : (int)UserStatusEnum.Inactive,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);

            var hashedPassword = HashPassword(password);
            var userAuth = new UserAuth
            {
                UserAuthID = Guid.NewGuid(),
                UserID = user.UserID,
                UserStatusId = isActive ? (int)UserStatusEnum.Active : (int)UserStatusEnum.Inactive,
                HashedPassword = hashedPassword,
                IsLocked = false,
                FailedLoginAttempts = 0
            };
            context.UserAuths.Add(userAuth);

            var roleEntity = context.Roles.FirstOrDefault(r => r.RoleName == role);
            if (roleEntity == null)
            {
                roleEntity = new Role { RoleID = Guid.NewGuid(), RoleName = role };
                context.Roles.Add(roleEntity);
            }

            var userRole = new UserRole
            {
                UserID = user.UserID,
                RoleID = roleEntity.RoleID
            };
            context.UserRoles.Add(userRole);

            context.SaveChanges();
            return user;
        }

        public static Hospital SeedHospital(AppDbContext context, Guid createdByUserId, string city = "Test City", string state = "Test State", bool isPubliclyListed = true, bool isActive = true)
        {
            var hospital = new Hospital
            {
                HospitalID = Guid.NewGuid(),
                Name = "Test Hospital",
                Type = "General",
                RegistrationNumber = $"REG{Guid.NewGuid():N}"[..12],
                Contact = "9999999999",
                Location = "123 Test Street",
                City = city,
                State = state,
                Country = "India",
                Pincode = "700001",
                IsActive = isActive,
                IsPubliclyListed = isPubliclyListed,
                CreatedByUserID = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            };
            context.Hospitals.Add(hospital);
            context.SaveChanges();
            return hospital;
        }

        public static Doctor SeedDoctor(AppDbContext context, User user, bool isPubliclyListed = false)
        {
            var doctor = new Doctor
            {
                DoctorID = Guid.NewGuid(),
                UserID = user.UserID,
                LicenseNumber = "DOC12345",
                Qualification = "MBBS",
                ExperienceYears = 5,
                MedicalCouncil = "Test Council",
                RegistrationYear = 2015,
                Bio = "Test Bio",
                CreatedAt = DateTime.UtcNow,
                ProfileCompletionPercent = 100,
                IsPubliclyListed = isPubliclyListed,
            };
            context.Doctors.Add(doctor);
            context.SaveChanges();
            return doctor;
        }

        // DoctorDepartments is the source of truth for "which hospital(s) a doctor belongs to" —
        // NOT the single retrofitted Doctor.HospitalId field (see GetDoctorFeesHandler). Tests that
        // need a doctor to resolve to a hospital in the public-directory handlers must seed this,
        // not just set doctor.HospitalId.
        public static DoctorDepartment SeedDoctorDepartment(AppDbContext context, Guid doctorId, Guid hospitalId, Guid? departmentId = null)
        {
            var doctorDepartment = new DoctorDepartment
            {
                DoctorDepartmentID = Guid.NewGuid(),
                DoctorID = doctorId,
                DepartmentID = departmentId ?? Guid.NewGuid(),
                HospitalId = hospitalId,
                AssignedAt = DateTime.UtcNow,
            };
            context.DoctorDepartments.Add(doctorDepartment);
            context.SaveChanges();
            return doctorDepartment;
        }

        public static UserAuth GetOrCreateUserAuth(AppDbContext context, User user, string otp = "123456", DateTime? otpExpireAt = null)
        {
            var userAuth = context.UserAuths.FirstOrDefault(ua => ua.UserID == user.UserID);
            if (userAuth == null)
            {
                userAuth = new UserAuth
                {
                    UserAuthID = Guid.NewGuid(),
                    UserID = user.UserID,
                    UserStatusId = user.UserStatusId,
                    IsLocked = false,
                    FailedLoginAttempts = 0
                };
                context.UserAuths.Add(userAuth);
            }

            userAuth.Otp = otp;
            userAuth.OtpExpireAt = otpExpireAt ?? DateTime.Now.AddMinutes(10);
            userAuth.IsOtpUsed = false;

            context.SaveChanges();
            return userAuth;
        }

        public static string HashPassword(string password)
        {
             var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
             return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }
    }


}
