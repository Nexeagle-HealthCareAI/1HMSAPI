using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetHospitalDoctorReviewsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalDoctorReviewsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalDoctorReviewsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorAtHospital_ReturnsHiddenAndVisibleReviews()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);

            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 5, Comment = "Great", IsHidden = false, CreatedAt = DateTime.UtcNow });
            _context.DoctorReviews.Add(new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID, Rating = 1, Comment = "Bad", IsHidden = true, CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalDoctorReviewsRequestModel { HospitalId = hospital.HospitalID, DoctorId = doctor.DoctorID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Reviews, Has.Count.EqualTo(2));
            // Average/count computed over visible reviews only.
            Assert.That(response.ReviewCount, Is.EqualTo(1));
            Assert.That(response.AverageRating, Is.EqualTo(5.0));
        }

        [Test]
        public async Task Handle_DoctorNotAtHospital_RejectsRequest()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            // No DoctorDepartment row links this doctor to the hospital.

            var response = await _handler.Handle(new GetHospitalDoctorReviewsRequestModel { HospitalId = Guid.NewGuid(), DoctorId = doctor.DoctorID }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
