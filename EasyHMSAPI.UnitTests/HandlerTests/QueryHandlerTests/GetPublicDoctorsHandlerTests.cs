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
using Microsoft.Extensions.Caching.Memory;
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

            _handler = new GetPublicDoctorsHandler(_context, _blobServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // GetPublicDoctorsHandler inner-joins UserProfiles -- TestDataFactory.SeedUser doesn't
        // create one, so any test expecting a non-empty result must add it explicitly.
        private void SeedProfile(User user, string fullName = "Dr. Test")
        {
            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = user.UserID,
                UserStatusId = user.UserStatusId,
                FullName = fullName,
                UpdatedAt = DateTime.UtcNow,
            });
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
            Assert.That(d.Address, Is.EqualTo(hospital.Location));
            Assert.That(d.City, Is.EqualTo(hospital.City));
            Assert.That(d.State, Is.EqualTo(hospital.State));
            Assert.That(d.Pincode, Is.EqualTo(hospital.Pincode));
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
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            var d = response.Doctors[0];
            Assert.That(d.Languages, Is.EquivalentTo(new[] { "English", "Hindi" }));
            Assert.That(d.Latitude, Is.EqualTo(22.5726m));
            Assert.That(d.Longitude, Is.EqualTo(88.3639m));
        }

        [Test]
        public async Task Handle_HospitalIdFilter_BypassesIsPubliclyListed_ButStillRequiresActive()
        {
            var user = TestDataFactory.SeedUser(_context);
            var notPubliclyListedHospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: false);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, notPubliclyListedHospital.HospitalID);
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { HospitalId = notPubliclyListedHospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].HospitalId, Is.EqualTo(notPubliclyListedHospital.HospitalID));
        }

        [Test]
        public async Task Handle_HospitalIdFilter_ExcludesDoctorsAtOtherHospitals()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "one@example.com", phone: "1111111111");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital1.HospitalID);
            SeedProfile(user1, "Dr. One");

            var user2 = TestDataFactory.SeedUser(_context, email: "two@example.com", phone: "2222222222");
            var hospital2 = TestDataFactory.SeedHospital(_context, user2.UserID);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital2.HospitalID);
            SeedProfile(user2, "Dr. Two");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { HospitalId = hospital1.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_ReturnsRatingAggregate_FromNonHiddenReviewsOnly()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 4, Comment = "x", CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 2, Comment = "x", CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 1, Comment = "x", IsHidden = true, CreatedAt = DateTime.UtcNow });
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            var d = response.Doctors[0];
            Assert.That(d.Rating, Is.EqualTo(3.0));
            Assert.That(d.ReviewCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_ReturnsOpdConsultFee_AtDoctorsCanonicalHospital()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorFees.Add(new DoctorFee { DoctorFeeId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, FeeType = "OPD_CONSULT", Amount = 500m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            // A different fee type must not be picked up as the consultation fee.
            _context.DoctorFees.Add(new DoctorFee { DoctorFeeId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, FeeType = "IPD_VISIT", Amount = 1200m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors[0].Fee, Is.EqualTo(500m));
        }

        [Test]
        public async Task Handle_InactiveOpdConsultFee_IsNotReturned()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorFees.Add(new DoctorFee { DoctorFeeId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, FeeType = "OPD_CONSULT", Amount = 500m, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors[0].Fee, Is.Null);
        }

        [Test]
        public async Task Handle_ExcludesHospitalResponses_FromRatingAggregate()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 4, Comment = "x", CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 1, Comment = "Thanks for the feedback.", IsHospitalResponse = true, CreatedAt = DateTime.UtcNow });
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            var d = response.Doctors[0];
            Assert.That(d.Rating, Is.EqualTo(4.0));
            Assert.That(d.ReviewCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_CmsForceListedDoctor_AppearsEvenAtNonPubliclyListedHospital()
        {
            // A CMS admin can set Doctor.IsPubliclyListed directly (DoctorRepository.
            // UpdateDoctorMarketingAsync), independent of whether the hospital itself has ever
            // opted in -- the whole point of that CMS control is to bypass a hospital that never
            // enabled its own public directory. So a hospital with at least one such doctor must
            // become eligible too, not just hospitals that opted in themselves.
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var listedHospital = TestDataFactory.SeedHospital(_context, user1.UserID, isPubliclyListed: true);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, listedHospital.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var unlistedHospital = TestDataFactory.SeedHospital(_context, user2.UserID, isPubliclyListed: false);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, unlistedHospital.HospitalID);

            SeedProfile(user1, "Dr. One");
            SeedProfile(user2, "Dr. Two");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(2));
            Assert.That(response.Doctors.Select(d => d.DoctorId), Is.EquivalentTo(new[] { doctor1.DoctorID, doctor2.DoctorID }));
        }

        [Test]
        public async Task Handle_ExcludesDoctorsFromNonPubliclyListedHospital_WhenDoctorAlsoNotListed()
        {
            // The hospital never opting in AND the doctor never being force-listed either --
            // still fully excluded, unlike the CMS-force-listed case above.
            var user = TestDataFactory.SeedUser(_context, email: "c@example.com", phone: "3333333333");
            var unlistedHospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: false);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, unlistedHospital.HospitalID);
            SeedProfile(user, "Dr. Neither");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel(), CancellationToken.None);

            Assert.That(response.Doctors, Is.Empty);
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

            SeedProfile(user1, "Dr. Listed");
            SeedProfile(user2, "Dr. Unlisted");
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

            SeedProfile(user1, "Dr. Kolkata");
            SeedProfile(user2, "Dr. Mumbai");
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

        [Test]
        public async Task Handle_DoctorIdFilter_ReturnsOnlyThatDoctor()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "g@example.com", phone: "8888888888");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital1.HospitalID);
            SeedProfile(user1, "Dr. Target");

            var user2 = TestDataFactory.SeedUser(_context, email: "h@example.com", phone: "9999999999");
            var hospital2 = TestDataFactory.SeedHospital(_context, user2.UserID);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital2.HospitalID);
            SeedProfile(user2, "Dr. Other");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { DoctorId = doctor1.DoctorID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_DoctorIdFilter_StillRequiresPubliclyListed()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            SeedProfile(user);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorsRequestModel { DoctorId = doctor.DoctorID }, CancellationToken.None);

            Assert.That(response.Doctors, Is.Empty);
        }
    }
}
