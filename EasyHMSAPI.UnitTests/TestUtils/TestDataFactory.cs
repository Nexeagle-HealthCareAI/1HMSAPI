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

        public static Doctor SeedDoctor(AppDbContext context, User user)
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
                ProfileCompletionPercent = 100
            };
            context.Doctors.Add(doctor);
            context.SaveChanges();
            return doctor;
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
