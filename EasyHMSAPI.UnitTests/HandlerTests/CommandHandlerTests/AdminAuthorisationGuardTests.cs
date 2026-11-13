using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Regression tests for the cross-tenant / any-role write holes: department create/update/toggle,
    /// hospital profile update, doctor-fee upsert and the global "is admin" check.
    /// </summary>
    [TestFixture]
    public class AdminAuthorisationGuardTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        /// <summary>A member of the hospital holding an Admin role owned by that hospital (admin_panel granted).</summary>
        private Guid SeedAdmin(Guid hospitalId, Guid? roleHospitalId = null, string roleName = "Admin")
        {
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = userId });
            _context.Roles.Add(new Role { RoleID = roleId, HospitalID = roleHospitalId ?? hospitalId, RoleName = roleName });
            _context.RolePermissions.Add(new RolePermission { RoleID = roleId, PermissionKey = "admin_panel", IsAllowed = true });
            _context.UserRoles.Add(new UserRole { UserID = userId, RoleID = roleId });
            _context.SaveChanges();
            return userId;
        }

        private Department SeedDepartment(Guid? hospitalId)
        {
            var d = new Department { DepartmentID = Guid.NewGuid(), HospitalID = hospitalId, Name = "Cardiology", IsActive = true, CreatedAt = DateTime.UtcNow };
            _context.Departments.Add(d);
            _context.SaveChanges();
            return d;
        }

        [Test]
        public async Task IsAdminAtHospital_AdminOfAnotherHospitalWhoIsOnlyStaffHere_IsNotAdmin()
        {
            var hospitalA = Guid.NewGuid();
            var hospitalB = Guid.NewGuid();
            var adminOfA = SeedAdmin(hospitalA);
            // Same person is merely a nurse (member, no admin role) at hospital B.
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalB, UserID = adminOfA });
            _context.SaveChanges();

            Assert.That(await CallerGuards.IsAdminAtHospitalAsync(_context, adminOfA, hospitalA, CancellationToken.None), Is.True);
            Assert.That(await CallerGuards.IsAdminAtHospitalAsync(_context, adminOfA, hospitalB, CancellationToken.None), Is.False);
        }

        [Test]
        public async Task IsAdminAtHospital_RequiresMembership()
        {
            var hospital = Guid.NewGuid();
            var outsider = SeedAdmin(Guid.NewGuid());

            Assert.That(await CallerGuards.IsAdminAtHospitalAsync(_context, outsider, hospital, CancellationToken.None), Is.False);
        }

        [Test]
        public async Task CreateDepartment_PlainMemberForbidden_AdminAllowedAndStampedAsCreator()
        {
            var hospital = Guid.NewGuid();
            var member = HrAuthSeed.SeedMember(_context, hospital);
            var admin = SeedAdmin(hospital);
            var handler = new CreateDepartmentHandler(_context);

            var denied = await handler.Handle(new CreateDepartmentRequestModel { HospitalID = hospital, Name = "X", CallerUserId = member }, CancellationToken.None);
            var allowed = await handler.Handle(new CreateDepartmentRequestModel { HospitalID = hospital, Name = "Y", CallerUserId = admin, CreatedByUserID = member }, CancellationToken.None);

            Assert.That(denied.Forbidden, Is.True);
            Assert.That(allowed.Forbidden, Is.False);
            var created = await _context.Departments.FirstAsync(d => d.DepartmentID == allowed.DepartmentID);
            Assert.That(created.CreatedByUserID, Is.EqualTo(admin), "creator comes from the verified caller, not the body");
            Assert.That(await _context.Departments.CountAsync(d => d.Name == "X"), Is.EqualTo(0));
        }

        [Test]
        public async Task UpdateAndToggleDepartment_GlobalDepartmentIsPlatformOnly()
        {
            var hospital = Guid.NewGuid();
            var admin = SeedAdmin(hospital);
            var global = SeedDepartment(null);

            var update = await new UpdateDepartmentHandler(_context).Handle(new UpdateDepartmentRequestModel { DepartmentId = global.DepartmentID, Name = "Hacked", CallerUserId = admin }, CancellationToken.None);
            var toggle = await new ToggleDepartmentStatusHandler(_context).Handle(new ToggleDepartmentStatusRequestModel { DepartmentId = global.DepartmentID, CallerUserId = admin }, CancellationToken.None);

            Assert.That(update.Forbidden, Is.True);
            Assert.That(toggle.Forbidden, Is.True);
            var reloaded = await _context.Departments.AsNoTracking().FirstAsync(d => d.DepartmentID == global.DepartmentID);
            Assert.That(reloaded.Name, Is.EqualTo("Cardiology"));
            Assert.That(reloaded.IsActive, Is.True);
        }

        [Test]
        public async Task UpdateAndToggleDepartment_OtherHospitalsDepartmentForbidden_OwnAllowed()
        {
            var hospitalA = Guid.NewGuid();
            var hospitalB = Guid.NewGuid();
            var adminA = SeedAdmin(hospitalA);
            var deptOfB = SeedDepartment(hospitalB);
            var deptOfA = SeedDepartment(hospitalA);
            var update = new UpdateDepartmentHandler(_context);
            var toggle = new ToggleDepartmentStatusHandler(_context);

            var crossUpdate = await update.Handle(new UpdateDepartmentRequestModel { DepartmentId = deptOfB.DepartmentID, Name = "Hacked", CallerUserId = adminA }, CancellationToken.None);
            var crossToggle = await toggle.Handle(new ToggleDepartmentStatusRequestModel { DepartmentId = deptOfB.DepartmentID, CallerUserId = adminA }, CancellationToken.None);
            var ownUpdate = await update.Handle(new UpdateDepartmentRequestModel { DepartmentId = deptOfA.DepartmentID, Name = "Renamed", CallerUserId = adminA }, CancellationToken.None);
            var ownToggle = await toggle.Handle(new ToggleDepartmentStatusRequestModel { DepartmentId = deptOfA.DepartmentID, CallerUserId = adminA }, CancellationToken.None);

            Assert.That(crossUpdate.Forbidden, Is.True);
            Assert.That(crossToggle.Forbidden, Is.True);
            Assert.That(ownUpdate.Forbidden, Is.False);
            Assert.That(ownUpdate.Name, Is.EqualTo("Renamed"));
            Assert.That(ownToggle.Forbidden, Is.False);
            Assert.That(ownToggle.IsActive, Is.False);
            Assert.That((await _context.Departments.AsNoTracking().FirstAsync(d => d.DepartmentID == deptOfB.DepartmentID)).Name, Is.EqualTo("Cardiology"));
        }

        [Test]
        public async Task UpdateDepartment_NoCaller_Forbidden()
        {
            var dept = SeedDepartment(Guid.NewGuid());

            var response = await new UpdateDepartmentHandler(_context).Handle(new UpdateDepartmentRequestModel { DepartmentId = dept.DepartmentID, Name = "Hacked" }, CancellationToken.None);

            Assert.That(response.Forbidden, Is.True);
        }

        [Test]
        public async Task UpdateHospital_PlainMemberForbidden_AdminAllowed()
        {
            var owner = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, owner.UserID);
            var member = HrAuthSeed.SeedMember(_context, hospital.HospitalID);
            var admin = SeedAdmin(hospital.HospitalID);
            var handler = new HospitalUpdateHandler(_context);

            var denied = await handler.Handle(new HospitalUpdateRequestModel { HospitalId = hospital.HospitalID, Name = "Pwned", CallerUserId = member }, CancellationToken.None);
            var allowed = await handler.Handle(new HospitalUpdateRequestModel { HospitalId = hospital.HospitalID, Name = "Renamed", CallerUserId = admin }, CancellationToken.None);

            Assert.That(denied.Forbidden, Is.True);
            Assert.That(allowed.Success, Is.True);
            Assert.That((await _context.Hospitals.AsNoTracking().FirstAsync(h => h.HospitalID == hospital.HospitalID)).Name, Is.EqualTo("Renamed"));
        }

        [Test]
        public async Task UpsertDoctorFee_PlainMemberForbidden_AdminAllowed()
        {
            var hospital = Guid.NewGuid();
            var doctorId = Guid.NewGuid();
            var receptionist = HrAuthSeed.SeedMember(_context, hospital, "appointment_booking");
            var admin = SeedAdmin(hospital);
            var handler = new UpsertDoctorFeeHandler(_context);

            var denied = await handler.Handle(new UpsertDoctorFeeRequestModel { HospitalId = hospital, DoctorId = doctorId, OpdConsultFee = 0, CallerUserId = receptionist }, CancellationToken.None);
            var allowed = await handler.Handle(new UpsertDoctorFeeRequestModel { HospitalId = hospital, DoctorId = doctorId, OpdConsultFee = 500, CallerUserId = admin }, CancellationToken.None);

            Assert.That(denied.Forbidden, Is.True);
            Assert.That(allowed.IsSuccess, Is.True);
            Assert.That(await _context.DoctorFees.CountAsync(f => f.DoctorId == doctorId && f.Amount == 500), Is.GreaterThan(0));
        }
    }
}
