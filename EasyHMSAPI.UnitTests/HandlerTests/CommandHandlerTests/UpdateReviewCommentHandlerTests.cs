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
    public class UpdateReviewCommentHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateReviewCommentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateReviewCommentHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ExistingRatingOnlyReview_AttachesComment()
        {
            var doctorId = Guid.NewGuid();
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), DoctorId = doctorId, Rating = 5, Comment = null, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateReviewCommentRequestModel { DoctorId = doctorId, ReviewId = review.ReviewId, Comment = "Actually, great experience!" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.DoctorReviews.FindAsync(review.ReviewId);
            Assert.That(updated!.Comment, Is.EqualTo("Actually, great experience!"));
            Assert.That(updated.Rating, Is.EqualTo(5), "Update must never touch the rating.");
        }

        [Test]
        public async Task Handle_BlankComment_ReturnsFailure()
        {
            var doctorId = Guid.NewGuid();
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), DoctorId = doctorId, Rating = 5, Comment = null, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateReviewCommentRequestModel { DoctorId = doctorId, ReviewId = review.ReviewId, Comment = "   " }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ReviewBelongsToDifferentDoctor_ReturnsFailure()
        {
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), Rating = 5, Comment = null, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateReviewCommentRequestModel { DoctorId = Guid.NewGuid(), ReviewId = review.ReviewId, Comment = "text" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var unchanged = await _context.DoctorReviews.FindAsync(review.ReviewId);
            Assert.That(unchanged!.Comment, Is.Null);
        }

        [Test]
        public async Task Handle_HospitalResponseRow_ReturnsFailure()
        {
            var doctorId = Guid.NewGuid();
            var review = new DoctorReview { ReviewId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), DoctorId = doctorId, Rating = 5, Comment = "Thanks for the feedback.", IsHospitalResponse = true, CreatedAt = DateTime.UtcNow };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateReviewCommentRequestModel { DoctorId = doctorId, ReviewId = review.ReviewId, Comment = "hijacked" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_UnknownReviewId_ReturnsFailure()
        {
            var response = await _handler.Handle(new UpdateReviewCommentRequestModel { DoctorId = Guid.NewGuid(), ReviewId = Guid.NewGuid(), Comment = "text" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
