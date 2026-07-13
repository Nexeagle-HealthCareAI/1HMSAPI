using System;
using System.Linq;
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
    public class AdmissionReferralCommentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private AdmissionReferralCommentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmissionReferralCommentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Guid referralId) SeedReferral()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var referralId = Guid.NewGuid();
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = referralId, HospitalId = hospitalId, PatientId = "PAT1",
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
            return (hospitalId, referralId);
        }

        [Test]
        public async Task Handle_ValidComment_IsAddedAndRetrievable()
        {
            var (hospitalId, referralId) = SeedReferral();

            var response = await _handler.Handle(new AddAdmissionReferralCommentRequestModel
            {
                HospitalId = hospitalId, ReferralId = referralId, CommentText = "Called family, awaiting confirmation.", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.CommentId, Is.Not.Null);

            var stored = _context.AdmissionReferralComment.Single(c => c.ReferralId == referralId);
            Assert.That(stored.CommentText, Is.EqualTo("Called family, awaiting confirmation."));
            Assert.That(stored.CreatedBy, Is.EqualTo("Front Desk"));
        }

        [Test]
        public async Task Handle_BlankCommentText_ReturnsFailure()
        {
            var (hospitalId, referralId) = SeedReferral();

            var response = await _handler.Handle(new AddAdmissionReferralCommentRequestModel
            {
                HospitalId = hospitalId, ReferralId = referralId, CommentText = "   ", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.AdmissionReferralComment.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_WrongHospitalId_ReturnsFailure()
        {
            var (_, referralId) = SeedReferral();

            var response = await _handler.Handle(new AddAdmissionReferralCommentRequestModel
            {
                HospitalId = Guid.NewGuid(), ReferralId = referralId, CommentText = "Test", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_ReferralNotFound_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();

            var response = await _handler.Handle(new AddAdmissionReferralCommentRequestModel
            {
                HospitalId = hospitalId, ReferralId = Guid.NewGuid(), CommentText = "Test", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
