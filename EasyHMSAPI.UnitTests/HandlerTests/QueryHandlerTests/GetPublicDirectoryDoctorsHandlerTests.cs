using System;
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
    public class GetPublicDirectoryDoctorsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetPublicDirectoryDoctorsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobServiceMock = new Mock<IBlobStorageService>();
            _blobServiceMock.Setup(x => x.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://example.com/photo.jpg");
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["BlobStorage:ProfilePhotosContainer"]).Returns("photos");

            _handler = new GetPublicDirectoryDoctorsHandler(_context, _blobServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_MissingHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicDirectoryDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ReturnsFullTileFieldSet_IncludingUnlistedDoctors()
        {
            // Arrange — a doctor NOT yet publicly listed must still show up, since the admin
            // needs to be able to edit/toggle it before it's public.
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            doctor.LanguagesJson = "[\"English\",\"Bengali\"]";
            doctor.PublicContactEmail = "doc@example.com";
            doctor.PublicContactPhone = "9999999999";
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

            // Act
            var response = await _handler.Handle(new GetPublicDirectoryDoctorsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            var d = response.Doctors[0];
            Assert.That(d.DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(d.FullName, Is.EqualTo("Dr. Jane Doe"));
            Assert.That(d.PhotoUrl, Is.EqualTo("http://example.com/photo.jpg"));
            Assert.That(d.LicenseNumber, Is.EqualTo(doctor.LicenseNumber));
            Assert.That(d.Qualification, Is.EqualTo(doctor.Qualification));
            Assert.That(d.Languages, Is.EquivalentTo(new[] { "English", "Bengali" }));
            Assert.That(d.PublicContactEmail, Is.EqualTo("doc@example.com"));
            Assert.That(d.PublicContactPhone, Is.EqualTo("9999999999"));
            Assert.That(d.IsPubliclyListed, Is.False);
        }

        [Test]
        public async Task Handle_ExcludesDoctorsFromOtherHospitals()
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

            var response = await _handler.Handle(new GetPublicDirectoryDoctorsRequestModel { HospitalId = hospital1.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_ReturnsRatingAggregate_FromNonHiddenReviewsOnly()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 5, Comment = "x", CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 3, Comment = "x", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDirectoryDoctorsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            var d = response.Doctors[0];
            Assert.That(d.Rating, Is.EqualTo(4.0));
            Assert.That(d.ReviewCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_ExcludesHospitalResponses_FromRatingAggregate()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 5, Comment = "x", CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 1, Comment = "Thanks for the feedback.", IsHospitalResponse = true, CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDirectoryDoctorsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            var d = response.Doctors[0];
            Assert.That(d.Rating, Is.EqualTo(5.0));
            Assert.That(d.ReviewCount, Is.EqualTo(1));
        }
    }
}
