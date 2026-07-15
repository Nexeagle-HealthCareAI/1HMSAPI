using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SubmitDoctorReviewHandlerTests
    {
        private AppDbContext _context = null!;
        private SubmitDoctorReviewHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SubmitDoctorReviewHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Guid SeedPublicDoctor()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            return doctor.DoctorID;
        }

        [Test]
        public async Task Handle_ValidReview_PersistsAndReturnsSuccess()
        {
            var doctorId = SeedPublicDoctor();
            var request = new SubmitDoctorReviewRequestModel
            {
                DoctorId = doctorId,
                AuthorName = "Priya",
                Rating = 5,
                Comment = "Excellent doctor.",
                IpAddress = "127.0.0.1",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ReviewId, Is.Not.Null);

            var saved = _context.DoctorReviews.Single();
            Assert.That(saved.DoctorId, Is.EqualTo(doctorId));
            Assert.That(saved.Rating, Is.EqualTo(5));
            Assert.That(saved.Comment, Is.EqualTo("Excellent doctor."));
            Assert.That(saved.AuthorName, Is.EqualTo("Priya"));
            Assert.That(saved.IsHidden, Is.False);
            Assert.That(saved.SubmittedIp, Is.EqualTo("127.0.0.1"));
        }

        [Test]
        public async Task Handle_BlankAuthorName_StoresNull()
        {
            var doctorId = SeedPublicDoctor();
            var request = new SubmitDoctorReviewRequestModel
            {
                DoctorId = doctorId,
                AuthorName = "   ",
                Rating = 4,
                Comment = "Good.",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.DoctorReviews.Single().AuthorName, Is.Null);
        }

        [TestCase(0)]
        [TestCase(6)]
        public async Task Handle_RatingOutOfRange_ReturnsFailure(int rating)
        {
            var doctorId = SeedPublicDoctor();
            var request = new SubmitDoctorReviewRequestModel { DoctorId = doctorId, Rating = rating, Comment = "x" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.DoctorReviews.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_BlankComment_ReturnsFailure()
        {
            var doctorId = SeedPublicDoctor();
            var request = new SubmitDoctorReviewRequestModel { DoctorId = doctorId, Rating = 5, Comment = "  " };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_DoctorNotPubliclyListed_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            var request = new SubmitDoctorReviewRequestModel { DoctorId = doctor.DoctorID, Rating = 5, Comment = "x" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found."));
        }
    }
}
