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
    public class GetAdmissionReferralCommentsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAdmissionReferralCommentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAdmissionReferralCommentsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsCommentsNewestFirst()
        {
            var hospitalId = Guid.NewGuid();
            var referralId = Guid.NewGuid();
            var earlier = DateTime.UtcNow.AddHours(-2);
            var later = DateTime.UtcNow;

            _context.AdmissionReferralComment.Add(new AdmissionReferralComment
            {
                CommentId = Guid.NewGuid(), ReferralId = referralId, HospitalId = hospitalId,
                CommentText = "First comment", CreatedAt = earlier, CreatedBy = "Nurse A",
            });
            _context.AdmissionReferralComment.Add(new AdmissionReferralComment
            {
                CommentId = Guid.NewGuid(), ReferralId = referralId, HospitalId = hospitalId,
                CommentText = "Second comment", CreatedAt = later, CreatedBy = "Nurse B",
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralCommentsRequestModel { HospitalId = hospitalId, ReferralId = referralId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Comments, Has.Count.EqualTo(2));
            Assert.That(response.Comments[0].CommentText, Is.EqualTo("Second comment"));
            Assert.That(response.Comments[1].CommentText, Is.EqualTo("First comment"));
        }

        [Test]
        public async Task Handle_NoComments_ReturnsEmptyList()
        {
            var response = await _handler.Handle(new GetAdmissionReferralCommentsRequestModel { HospitalId = Guid.NewGuid(), ReferralId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Comments, Is.Empty);
        }
    }
}
