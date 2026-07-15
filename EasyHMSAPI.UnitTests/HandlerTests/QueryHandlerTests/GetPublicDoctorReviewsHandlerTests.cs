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
    public class GetPublicDoctorReviewsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicDoctorReviewsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicDoctorReviewsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private DoctorReview MakeReview(Guid doctorId, int rating, bool isHidden = false) => new DoctorReview
        {
            ReviewId = Guid.NewGuid(),
            HospitalId = Guid.NewGuid(),
            DoctorId = doctorId,
            Rating = rating,
            Comment = "Comment",
            IsHidden = isHidden,
            CreatedAt = DateTime.UtcNow,
        };

        [Test]
        public async Task Handle_ExcludesHiddenReviews_FromListAndAverage()
        {
            var doctorId = Guid.NewGuid();
            _context.DoctorReviews.Add(MakeReview(doctorId, 5));
            _context.DoctorReviews.Add(MakeReview(doctorId, 3));
            _context.DoctorReviews.Add(MakeReview(doctorId, 1, isHidden: true));
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorReviewsRequestModel { DoctorId = doctorId }, CancellationToken.None);

            Assert.That(response.Reviews, Has.Count.EqualTo(2));
            Assert.That(response.ReviewCount, Is.EqualTo(2));
            Assert.That(response.AverageRating, Is.EqualTo(4.0));
        }

        [Test]
        public async Task Handle_NoReviews_ReturnsZeroedAggregate()
        {
            var response = await _handler.Handle(new GetPublicDoctorReviewsRequestModel { DoctorId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Reviews, Is.Empty);
            Assert.That(response.ReviewCount, Is.EqualTo(0));
            Assert.That(response.AverageRating, Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_ExcludesReviewsForOtherDoctors()
        {
            var doctorId = Guid.NewGuid();
            _context.DoctorReviews.Add(MakeReview(doctorId, 5));
            _context.DoctorReviews.Add(MakeReview(Guid.NewGuid(), 2));
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorReviewsRequestModel { DoctorId = doctorId }, CancellationToken.None);

            Assert.That(response.Reviews, Has.Count.EqualTo(1));
        }
    }
}
