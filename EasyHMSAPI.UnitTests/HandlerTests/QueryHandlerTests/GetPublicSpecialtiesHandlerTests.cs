using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPublicSpecialtiesHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicSpecialtiesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicSpecialtiesHandler(_context, new MemoryCache(new MemoryCacheOptions()));
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
            _context.SaveChanges();
            return speciality;
        }

        [Test]
        public async Task Handle_ReturnsCategory_ForPubliclyBookableDoctor()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiologist", "Cardiologist");
            doctor.PrimaryMedicalSpecialityId = speciality.SpecialityId;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Specialties, Has.Count.EqualTo(1));
            Assert.That(response.Specialties[0].Category, Is.EqualTo("Cardiologist"));
            Assert.That(response.Specialties[0].DisplayName, Is.EqualTo("Cardiologist"));
            Assert.That(response.Specialties[0].DoctorCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_IncludesCategory_WhenDoctorCmsForceListed_EvenIfHospitalNotPubliclyListed()
        {
            // Same CMS-override rule as GetPublicDoctorsHandler: a doctor set publicly listed
            // directly from CMS makes their hospital eligible too, even if the hospital itself
            // never opted in.
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: false);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiologist", "Cardiologist");
            doctor.PrimaryMedicalSpecialityId = speciality.SpecialityId;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Specialties, Has.Count.EqualTo(1));
            Assert.That(response.Specialties[0].Category, Is.EqualTo("Cardiologist"));
        }

        [Test]
        public async Task Handle_ExcludesCategory_WhenHospitalNotPubliclyListed_AndDoctorNotForceListed()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: false);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiologist", "Cardiologist");
            doctor.PrimaryMedicalSpecialityId = speciality.SpecialityId;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Specialties, Is.Empty);
        }

        [Test]
        public async Task Handle_ExcludesCategory_WhenDoctorNotPubliclyListed()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiologist", "Cardiologist");
            doctor.PrimaryMedicalSpecialityId = speciality.SpecialityId;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Specialties, Is.Empty);
        }

        [Test]
        public async Task Handle_GroupsMultipleDoctors_UnderSameCategory_AndCountsThem()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var hospital = TestDataFactory.SeedHospital(_context, user1.UserID, isPubliclyListed: true);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital.HospitalID);
            var speciality = SeedSpeciality("Cardiologist", "Cardiologist");
            doctor1.PrimaryMedicalSpecialityId = speciality.SpecialityId;

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital.HospitalID);
            doctor2.PrimaryMedicalSpecialityId = speciality.SpecialityId;

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Specialties, Has.Count.EqualTo(1));
            Assert.That(response.Specialties[0].DoctorCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_ExcludesDoctor_WithNoPrimarySpeciality()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicSpecialtiesRequestModel(), CancellationToken.None);

            Assert.That(response.Specialties, Is.Empty);
        }
    }
}
