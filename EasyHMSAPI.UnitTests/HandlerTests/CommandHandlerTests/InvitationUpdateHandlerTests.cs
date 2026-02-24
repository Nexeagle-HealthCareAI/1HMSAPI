using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class InvitationUpdateHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private Mock<IEmailService> _emailServiceMock = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private InvitationUpdateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _emailServiceMock = new Mock<IEmailService>();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            _configurationMock = new Mock<IConfiguration>();

             _configurationMock.SetupGet(x => x["Invitation:RegistrationBaseUrl"]).Returns("http://test.com/reg?token=");

            _handler = new InvitationUpdateHandler(
                _context, 
                _smsServiceMock.Object, 
                _emailServiceMock.Object, 
                _whatsAppServiceMock.Object, 
                _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ResendScope_ResendsInvitation()
        {
            // Arrange
            var invitationId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            
            // Seed Hospital and Role to avoid nulls when fetching names
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "h@h.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var roleId = Guid.NewGuid();
            var role = new Role { RoleID = roleId, RoleName = "Doctor" };
            _context.Roles.Add(role);

            var invitation = new UserInvitation
            {
                InvitationID = invitationId,
                HospitalID = hospitalId,
                InvitedByUserID = Guid.NewGuid(),
                RoleID = roleId,
                RecipientMobile = "1234567890",
                RecipientEmail = "test@test.com",
                TokenHash = new byte[32],
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Status = "Pending"
            };
            _context.UserInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            var request = new InvitationUpdateRequestModel { InvitationId = invitationId, Scope = "resend" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Invitation link resent."));
            
            _whatsAppServiceMock.Verify(x => x.SendInvitationAsync(invitation.RecipientMobile, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task Handle_RevokeScope_RevokesInvitation()
        {
            // Arrange
            var invitationId = Guid.NewGuid();
            var invitation = new UserInvitation
            {
                InvitationID = invitationId,
                HospitalID = Guid.NewGuid(),
                InvitedByUserID = Guid.NewGuid(),
                RoleID = Guid.NewGuid(),
                RecipientMobile = "1234567890",
                TokenHash = new byte[32],
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Status = "Pending"
            };
            _context.UserInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            var request = new InvitationUpdateRequestModel { InvitationId = invitationId, Scope = "revoke", PerformedByUserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Status, Is.EqualTo("Revoked"));
            
            var updatedInv = await _context.UserInvitations.FindAsync(invitationId);
            Assert.That(updatedInv!.Status, Is.EqualTo("Revoked"));
            Assert.That(updatedInv.RevokedAt, Is.Not.Null);
        }
        
        [Test]
        public async Task Handle_InvalidScope_ReturnsFailure()
        {
             // Arrange
            var invitationId = Guid.NewGuid();
             var invitation = new UserInvitation { InvitationID = invitationId, HospitalID = Guid.NewGuid(), InvitedByUserID = Guid.NewGuid(), RoleID = Guid.NewGuid(), RecipientMobile = "1234567890", TokenHash = new byte[32], ExpiresAt = DateTime.UtcNow.AddDays(7), Status = "Pending" };
             _context.UserInvitations.Add(invitation);
             await _context.SaveChangesAsync();

            var request = new InvitationUpdateRequestModel { InvitationId = invitationId, Scope = "invalid" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid scope"));
        }
    }
}
