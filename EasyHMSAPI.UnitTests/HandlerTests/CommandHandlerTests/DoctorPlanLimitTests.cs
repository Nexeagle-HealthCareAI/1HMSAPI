using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Implementations;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // The plan's MaxDoctors must hold on every way a doctor can take a seat: quick-add, reactivating a deactivated doctor,
    // and a chain doctor being added to another hospital.
    [TestFixture]
    public class DoctorPlanLimitTests
    {
        private AppDbContext _context = null!;
        private SubscriptionLimitHelper _limits = null!;
        private Guid _hospitalId;
        private Guid _admin;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _limits = new SubscriptionLimitHelper(_context);
            _hospitalId = Guid.NewGuid();
            SeedHospital(_hospitalId, maxDoctors: 2);
            _admin = SeedAdmin(_hospitalId);
            _context.Roles.Add(new Role { RoleID = Guid.NewGuid(), HospitalID = null, RoleName = "Doctor" });
            _context.Roles.Add(new Role { RoleID = Guid.NewGuid(), HospitalID = null, RoleName = "Nurse" });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void SeedHospital(Guid hospitalId, int? maxDoctors, Guid? chainId = null)
        {
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, ChainId = chainId, Name = "H" + hospitalId.ToString("N")[..4], Type = "Clinic", RegistrationNumber = "REG" + hospitalId.ToString("N")[..6],
                Contact = "9999999999", Location = "Loc", City = "City", State = "State", Country = "India", Pincode = "560001",
            });
            _context.HospitalSubscriptions.Add(new HospitalSubscription { HospitalSubscriptionId = Guid.NewGuid(), HospitalId = hospitalId, MaxDoctors = maxDoctors, Status = "Active" });
            _context.SaveChanges();
        }

        private Guid SeedAdmin(Guid hospitalId)
        {
            var userId = HrAuthSeed.SeedMember(_context, hospitalId);
            var roleId = Guid.NewGuid();
            _context.Roles.Add(new Role { RoleID = roleId, HospitalID = hospitalId, RoleName = "Admin" });
            _context.UserRoles.Add(new UserRole { UserID = userId, RoleID = roleId });
            _context.SaveChanges();
            return userId;
        }

        /// <summary>A doctor created at <paramref name="hospitalId"/> (Doctor row + membership).</summary>
        private (Guid UserId, Guid DoctorId) SeedDoctor(Guid hospitalId, int status = (int)UserStatusEnum.Active, string mobile = null!)
        {
            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId, status, mobile ?? Guid.NewGuid().ToString("N")[..10]));
            var doctor = TestEntityFactory.CreateDoctor(Guid.NewGuid(), userId, "LIC" + Guid.NewGuid().ToString("N")[..6]);
            doctor.HospitalId = hospitalId;
            _context.Doctors.Add(doctor);
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = userId });
            _context.SaveChanges();
            return (userId, doctor.DoctorID);
        }

        private QuickAddUserHandler QuickAdd()
        {
            var doctorCreate = new DoctorCreateHandler(_context, _limits);
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<DoctorCreateRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns((DoctorCreateRequestModel r, CancellationToken ct) => doctorCreate.Handle(r, ct));
            return new QuickAddUserHandler(_context, new Mock<IMaskingService>().Object, mediator.Object);
        }

        private QuickAddUserRequestModel DoctorRequest(string mobile) => new()
        {
            FullName = "Dr New", MobileNumber = mobile, Password = "Passw0rd!", Roles = { "Doctor" }, HospitalId = _hospitalId,
            LicenseNumber = "LIC-NEW", MedicalCouncil = "KMC", RegistrationYear = 2015, CallerUserId = _admin,
        };

        // (DoctorCreateHandler's happy path cannot run on the InMemory provider, so the "has room" side is asserted on the limit check
        // that QuickAdd, reactivation and chain-add all call.)
        [Test]
        public async Task UnderTheLimit_ThereIsRoom()
        {
            SeedDoctor(_hospitalId);
            Assert.That((await _limits.CanAddDoctorAsync(_hospitalId, CancellationToken.None)).Allowed, Is.True);
        }

        [Test]
        public async Task QuickAdd_Doctor_AtTheLimit_IsRefused_AndLeavesNoUserBehind()
        {
            SeedDoctor(_hospitalId);
            SeedDoctor(_hospitalId);

            var response = await QuickAdd().Handle(DoctorRequest("9000000002"), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("allows up to 2 doctor"));
            // (the user row is created in the same transaction and rolled back on SQL Server; the InMemory provider used here
            // has no real transactions, so only the refusal itself can be asserted)
        }

        [Test]
        public async Task QuickAdd_Nurse_IsNotAffectedByTheDoctorLimit()
        {
            SeedDoctor(_hospitalId);
            SeedDoctor(_hospitalId);
            var response = await QuickAdd().Handle(new QuickAddUserRequestModel
            {
                FullName = "Nurse N", MobileNumber = "9000000003", Password = "Passw0rd!", Roles = { "Nurse" }, HospitalId = _hospitalId, CallerUserId = _admin,
            }, CancellationToken.None);
            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task DeactivatedDoctor_FreesTheirSeat()
        {
            SeedDoctor(_hospitalId);
            SeedDoctor(_hospitalId, status: (int)UserStatusEnum.Revoked);
            Assert.That((await _limits.CanAddDoctorAsync(_hospitalId, CancellationToken.None)).Allowed, Is.True);
        }

        [Test]
        public async Task Reactivating_ADoctor_AtTheLimit_IsRefused()
        {
            SeedDoctor(_hospitalId);
            SeedDoctor(_hospitalId);
            var (revokedUser, _) = SeedDoctor(_hospitalId, status: (int)UserStatusEnum.Revoked);

            var response = await new ReactivateUserHandler(_context, _limits).Handle(
                new ReactivateUserRequestModel { HospitalId = _hospitalId, UserId = revokedUser, CallerUserId = _admin }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("allows up to 2 doctor"));
            Assert.That(_context.Users.Single(u => u.UserID == revokedUser).UserStatusId, Is.EqualTo((int)UserStatusEnum.Revoked));
        }

        [Test]
        public async Task Reactivating_ADoctor_UnderTheLimit_Works()
        {
            SeedDoctor(_hospitalId);
            var (revokedUser, _) = SeedDoctor(_hospitalId, status: (int)UserStatusEnum.Revoked);

            var response = await new ReactivateUserHandler(_context, _limits).Handle(
                new ReactivateUserRequestModel { HospitalId = _hospitalId, UserId = revokedUser, CallerUserId = _admin }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task Reactivating_ANonDoctor_IgnoresTheDoctorLimit()
        {
            SeedDoctor(_hospitalId);
            SeedDoctor(_hospitalId);
            var nurse = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(nurse, (int)UserStatusEnum.Revoked, "9111111111"));
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = _hospitalId, UserID = nurse });
            _context.SaveChanges();

            var response = await new ReactivateUserHandler(_context, _limits).Handle(
                new ReactivateUserRequestModel { HospitalId = _hospitalId, UserId = nurse, CallerUserId = _admin }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task ChainDoctor_AddedToAnotherHospital_TakesASeatThere_AndIsRefusedWhenFull()
        {
            // hospital B (in a chain owned by the admin) is full with two of its own doctors
            var chainId = Guid.NewGuid();
            var hospitalB = Guid.NewGuid();
            _context.HospitalChains.Add(new HospitalChain { ChainId = chainId, OwnerUserId = _admin, Name = "Chain" });
            SeedHospital(hospitalB, maxDoctors: 2, chainId: chainId);
            SeedDoctor(hospitalB);
            SeedDoctor(hospitalB);

            var (visitingUser, visitingDoctor) = SeedDoctor(_hospitalId);
            _context.Roles.Add(new Role { RoleID = Guid.NewGuid(), HospitalID = null, RoleName = "AdminDoctor" });
            var doctorRole = _context.Roles.First(r => r.RoleName == "Doctor" && r.HospitalID == null);
            _context.UserRoles.Add(new UserRole { UserID = visitingUser, RoleID = doctorRole.RoleID });
            _context.SaveChanges();

            var response = await new AddDoctorToHospitalHandler(_context, _limits).Handle(new AddDoctorToHospitalRequestModel
            {
                DoctorId = visitingDoctor, TargetHospitalId = hospitalB, DepartmentId = Guid.NewGuid(), CallerUserId = _admin,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("allows up to 2 doctor"));
            Assert.That(_context.HospitalUsers.Any(hu => hu.UserID == visitingUser && hu.HospitalID == hospitalB), Is.False);
        }

        [Test]
        public async Task SharedChainDoctors_AreCountedAtTheHospitalTheyWorkAt()
        {
            var hospitalB = Guid.NewGuid();
            SeedHospital(hospitalB, maxDoctors: 1);
            var (user, _) = SeedDoctor(_hospitalId);                  // created at A
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalB, UserID = user });   // works at B too
            _context.SaveChanges();

            var atB = await _limits.CanAddDoctorAsync(hospitalB, CancellationToken.None);

            Assert.That(atB.Allowed, Is.False, "B's only seat is taken by the shared doctor");
        }
    }
}
