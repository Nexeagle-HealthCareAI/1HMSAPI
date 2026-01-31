using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class InvitationMapUserHandlerTests
    {
        private AppDbContext _context = null!;
        private InvitationMapUserHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new InvitationMapUserHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_MapsUser()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var hospitalId = Guid.NewGuid();
            var invitationId = Guid.NewGuid();
            var invitation = new UserInvitation 
            { 
                 InvitationID = invitationId, 
                 HospitalID = hospitalId,
                 InvitedByUserID = Guid.NewGuid(),
                 RoleID = Guid.NewGuid(),
                 RecipientEmail = user.Email,
                 RecipientMobile = "1234567890",
                 TokenHash = new byte[32],
                 ExpiresAt = DateTime.UtcNow.AddDays(7),
                 Status = "Pending"
            };
            _context.UserInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            var request = new InvitationMapUserRequestModel
            {
                InvitationId = invitationId,
                UserId = user.UserID
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var hospitalUser = await _context.HospitalUsers.FirstOrDefaultAsync(hu => hu.HospitalID == hospitalId && hu.UserID == user.UserID);
            Assert.That(hospitalUser, Is.Not.Null);

            var updatedInv = await _context.UserInvitations.FindAsync(invitationId);
            Assert.That(updatedInv!.Status, Is.EqualTo("Accepted"));
        }

        [Test]
        public async Task Handle_InvitationNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new InvitationMapUserRequestModel { InvitationId = Guid.NewGuid(), UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invitation not found"));
        }
    }
}
