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
    public class MarkReviewHelpfulHandlerTests
    {
        private AppDbContext _context = null!;
        private MarkReviewHelpfulHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new MarkReviewHelpfulHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ExistingReview_IncrementsHelpfulCount()
        {
            var review = new DoctorReview
            {
                ReviewId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                Rating = 5,
                Comment = "x",
                HelpfulCount = 2,
                CreatedAt = DateTime.UtcNow,
            };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new MarkReviewHelpfulRequestModel { ReviewId = review.ReviewId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HelpfulCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Handle_ReviewNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new MarkReviewHelpfulRequestModel { ReviewId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
