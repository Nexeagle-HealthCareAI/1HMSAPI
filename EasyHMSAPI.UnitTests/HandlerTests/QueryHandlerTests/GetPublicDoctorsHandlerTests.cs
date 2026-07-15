using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPublicDoctorsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetPublicDoctorsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobServiceMock = new Mock<IBlobStorageService>();
            _blobServiceMock.Setup(x => x.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://example.com/photo.jpg");
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["BlobStorage:ProfilePhotosContainer"]).Returns("photos");

            _handler = new GetPublicDoctorsHandler(_context, _blobServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsPublicSafeFields_WithPhotoUrl_NoLicenseOrInternalFields()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            doctor.Bio = "Cardiologist with 10 years experience";
            await _context.SaveChangesAsync();

            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = user.UserID,
                UserStatusId = user.UserStatusId,
                FullName = "Dr. Jane Doe",
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            var d = response.Doctors[0];
            Assert.That(d.DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(d.FullName, Is.EqualTo("Dr. Jane Doe"));
            Assert.That(d.PhotoUrl, Is.EqualTo("http://example.com/photo.jpg"));
            Assert.That(d.Bio, Is.EqualTo("Cardiologist with 10 years experience"));
            Assert.That(d.HospitalId, Is.EqualTo(hospital.HospitalID));
            Assert.That(d.HospitalName, Is.EqualTo(hospital.Name));
            Assert.That(d.City, Is.EqualTo(hospital.City));
            Assert.That(d.State, Is.EqualTo(hospital.State));
        }

        [Test]
        public async Task Handle_ReturnsLanguages_AndHospitalGeolocation()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            hospital.Latitude = 22.5726m;
            hospital.Longitude = 88.3639m;
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            doctor.LanguagesJson = "[\"English\",\"Hindi\"]";
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            var d = response.Doctors[0];
            Assert.That(d.Languages, Is.EquivalentTo(new[] { "English", "Hindi" }));
            Assert.That(d.Latitude, Is.EqualTo(22.5726m));
            Assert.That(d.Longitude, Is.EqualTo(88.3639m));
        }

        [Test]
        public async Task Handle_ExcludesDoctorsFromNonPubliclyListedHospitals()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var listedHospital = TestDataFactory.SeedHospital(_context, user1.UserID, isPubliclyListed: true);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, listedHospital.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var unlistedHospital = TestDataFactory.SeedHospital(_context, user2.UserID, isPubliclyListed: false);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, unlistedHospital.HospitalID);

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_ExcludesDoctorsNotPubliclyListedThemselves()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "e@example.com", phone: "6666666666");
            var hospital = TestDataFactory.SeedHospital(_context, user1.UserID, isPubliclyListed: true);
            var listedDoctor = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, listedDoctor.DoctorID, hospital.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "f@example.com", phone: "7777777777");
            var unlistedDoctor = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, unlistedDoctor.DoctorID, hospital.HospitalID);

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(listedDoctor.DoctorID));
        }

        [Test]
        public async Task Handle_ReturnsDoctorsAcrossMultiplePubliclyListedHospitals()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "c@example.com", phone: "3333333333");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID, city: "Kolkata", state: "West Bengal");
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital1.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "d@example.com", phone: "4444444444");
            var hospital2 = TestDataFactory.SeedHospital(_context, user2.UserID, city: "Mumbai", state: "Maharashtra");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital2.HospitalID);

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(2));
            Assert.That(response.Doctors.Select(d => d.HospitalId), Is.EquivalentTo(new[] { hospital1.HospitalID, hospital2.HospitalID }));
        }

        [Test]
        public async Task Handle_ExcludesDoctorsFromInactiveHospital()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true, isActive: false);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Is.Empty);
        }

        [Test]
        public async Task Handle_ExcludesDoctorsWithNoHospitalDepartmentAssignment()
        {
            var user = TestDataFactory.SeedUser(_context);
            TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            // No SeedDoctorDepartment call — doctor has no hospital affiliation at all.
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Is.Empty);
        }
    }
}
