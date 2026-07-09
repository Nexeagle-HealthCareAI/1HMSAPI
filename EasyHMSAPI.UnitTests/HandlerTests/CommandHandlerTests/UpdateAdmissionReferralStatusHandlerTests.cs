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
    public class UpdateAdmissionReferralStatusHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateAdmissionReferralStatusHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateAdmissionReferralStatusHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private AdmissionReferral SeedReferral(Guid hospitalId, Guid doctorId, string status = "PENDING")
        {
            var referral = new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123",
                ReferringDoctorId = doctorId, CaseType = "PLANNED", StatusCode = status,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionReferrals.Add(referral);
            _context.SaveChanges();
            return referral;
        }

        [Test]
        public async Task Handle_NotAdmitted_RequiresReason()
        {
            var referral = SeedReferral(Guid.NewGuid(), Guid.NewGuid());

            var response = await _handler.Handle(new UpdateAdmissionReferralStatusRequestModel
            {
                ReferralId = referral.ReferralId,
                StatusCode = "NOT_ADMITTED",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("NotAdmittedReason"));
        }

        [Test]
        public async Task Handle_NotAdmitted_ValidRequest_UpdatesStatusAndAppendsHistory()
        {
            var referral = SeedReferral(Guid.NewGuid(), Guid.NewGuid());

            var response = await _handler.Handle(new UpdateAdmissionReferralStatusRequestModel
            {
                ReferralId = referral.ReferralId,
                StatusCode = "NOT_ADMITTED",
                NotAdmittedReason = "Patient declined surgery",
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.AdmissionReferrals.FindAsync(referral.ReferralId);
            Assert.That(updated!.StatusCode, Is.EqualTo("NOT_ADMITTED"));
            Assert.That(updated.NotAdmittedReason, Is.EqualTo("Patient declined surgery"));

            var history = _context.AdmissionReferralStatusHistories.Where(h => h.ReferralId == referral.ReferralId).ToList();
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].StatusCode, Is.EqualTo("NOT_ADMITTED"));
        }

        [Test]
        public async Task Handle_FollowUp_RequiresDate()
        {
            var referral = SeedReferral(Guid.NewGuid(), Guid.NewGuid());

            var response = await _handler.Handle(new UpdateAdmissionReferralStatusRequestModel
            {
                ReferralId = referral.ReferralId,
                StatusCode = "FOLLOW_UP",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("FollowUpDate"));
        }

        [Test]
        public async Task Handle_AlreadyConverted_ReturnsError()
        {
            var referral = SeedReferral(Guid.NewGuid(), Guid.NewGuid(), status: "CONVERTED");

            var response = await _handler.Handle(new UpdateAdmissionReferralStatusRequestModel
            {
                ReferralId = referral.ReferralId,
                StatusCode = "NOT_ADMITTED",
                NotAdmittedReason = "Too late",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already been converted"));
        }

        [Test]
        public async Task Handle_ReferralNotFound_ReturnsError()
        {
            var response = await _handler.Handle(new UpdateAdmissionReferralStatusRequestModel
            {
                ReferralId = Guid.NewGuid(),
                StatusCode = "PENDING",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
