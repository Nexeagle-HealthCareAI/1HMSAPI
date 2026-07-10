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
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            doctor.HospitalId = hospitalId;
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

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            var d = response.Doctors[0];
            Assert.That(d.DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(d.FullName, Is.EqualTo("Dr. Jane Doe"));
            Assert.That(d.PhotoUrl, Is.EqualTo("http://example.com/photo.jpg"));
            Assert.That(d.Bio, Is.EqualTo("Cardiologist with 10 years experience"));
        }

        [Test]
        public async Task Handle_ScopesToRequestedHospital_ExcludesOtherHospitalsDoctors()
        {
            var hospitalId = Guid.NewGuid();
            var otherHospitalId = Guid.NewGuid();

            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1);
            doctor1.HospitalId = hospitalId;

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);
            doctor2.HospitalId = otherHospitalId;

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }
    }
}
