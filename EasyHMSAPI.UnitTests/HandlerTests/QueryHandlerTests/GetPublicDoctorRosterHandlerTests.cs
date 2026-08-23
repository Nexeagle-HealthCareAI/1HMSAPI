using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPublicDoctorRosterHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicDoctorRosterHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicDoctorRosterHandler(_context, new MemoryCache(new MemoryCacheOptions()));
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private MedicalSpeciality SeedSpeciality(string category, string patientFacingName)
        {
            var speciality = new MedicalSpeciality
            {
                SpecialityId = Guid.NewGuid(),
                QualificationTypeCode = "MD",
                Name = patientFacingName,
                PatientFacingName = patientFacingName,
                PatientFacingCategory = category,
                IsActive = true,
            };
            _context.MedicalSpecialities.Add(speciality);
            return speciality;
        }

        [Test]
        public async Task Handle_ReturnsDoctorViaDoctorDepartments_WithDepartmentAndSpecialtyCategory()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var department = TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiology", "Cardiologist");
            doctor.PrimaryDepartmentID = department.DepartmentID;
            doctor.PrimaryMedicalSpecialityId = speciality.SpecialityId;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(response.Doctors[0].SpecialtyCategory, Is.EqualTo("Cardiology"));
        }

        [Test]
        public async Task Handle_BypassesIsPubliclyListed()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor.DoctorID));
        }

        [Test]
        public async Task Handle_ExcludesRevokedUser()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            user.UserStatusId = (int)UserStatusEnum.Revoked;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Is.Empty);
        }

        [Test]
        public async Task Handle_IncludesInactiveUser()
        {
            var user = TestDataFactory.SeedUser(_context, isActive: false);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor.DoctorID));
        }

        [Test]
        public async Task Handle_ExcludesDoctorsAtOtherHospitals()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital1.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var hospital2 = TestDataFactory.SeedHospital(_context, user2.UserID);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital2.HospitalID);

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital1.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_MissingHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_InactiveHospital_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isActive: false);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_CachesPerHospitalId()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var first = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);
            Assert.That(first.Doctors, Has.Count.EqualTo(1));

            var user2 = TestDataFactory.SeedUser(_context, email: "new-hire@example.com", phone: "3333333333");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var second = await _handler.Handle(new GetPublicDoctorRosterRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(second.Doctors, Has.Count.EqualTo(1)); // still the cached 60s-old response, not the new hire
        }
    }
}
