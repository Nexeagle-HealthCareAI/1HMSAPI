using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ModerateDoctorReviewHandlerTests
    {
        private AppDbContext _context = null!;
        private ModerateDoctorReviewHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ModerateDoctorReviewHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_HideReview_SetsIsHidden()
        {
            var hospitalId = Guid.NewGuid();
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = hospitalId, DoctorId = Guid.NewGuid(), Rating = 3, Comment = "x", IsHidden = false, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new ModerateDoctorReviewRequestModel { HospitalId = hospitalId, ReviewId = review.ReviewId, IsHidden = true }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.DoctorReviews.FindAsync(review.ReviewId);
            Assert.That(updated!.IsHidden, Is.True);
        }

        [Test]
        public async Task Handle_ReviewBelongsToDifferentHospital_RejectsRequest()
        {
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), Rating = 3, Comment = "x", IsHidden = false, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new ModerateDoctorReviewRequestModel { HospitalId = Guid.NewGuid(), ReviewId = review.ReviewId, IsHidden = true }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var unchanged = await _context.DoctorReviews.FindAsync(review.ReviewId);
            Assert.That(unchanged!.IsHidden, Is.False);
        }
    }
}
