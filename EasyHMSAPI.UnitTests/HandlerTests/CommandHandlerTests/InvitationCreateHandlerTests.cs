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
    public class InvitationCreateHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private Mock<IEmailService> _emailServiceMock = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private InvitationCreateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _emailServiceMock = new Mock<IEmailService>();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            _configurationMock = new Mock<IConfiguration>();

            _configurationMock.SetupGet(x => x["Invitation:RegistrationBaseUrl"]).Returns("http://test.com/reg?token=");

            _handler = new InvitationCreateHandler(
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
        public async Task Handle_ValidRequest_CreatesInvitation()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "h@h.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var roleId = Guid.NewGuid();
            var role = new Role { RoleID = roleId, RoleName = "Doctor" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            var request = new InvitationCreateRequestModel
            {
                HospitalId = hospitalId,
                RoleId = roleId,
                InvitedByUserId = Guid.NewGuid(),
                Name = "Invitee",
                Mobile = "1234567890",
                Email = "invitee@test.com"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.InvitationId, Is.Not.EqualTo(Guid.Empty));
            
            var invitation = await _context.UserInvitations.FirstOrDefaultAsync(i => i.InvitationID == response.InvitationId);
            Assert.That(invitation, Is.Not.Null);
            Assert.That(invitation!.Status, Is.EqualTo("Pending"));

            _whatsAppServiceMock.Verify(x => x.SendInvitationAsync(request.Mobile, hospital.Name, role.RoleName, It.IsAny<string>()), Times.Once);
            _emailServiceMock.Verify(x => x.SendInvitationEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task Handle_UserExists_ReturnsFailure()
        {
             // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "h@h.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var roleId = Guid.NewGuid();
            var role = new Role { RoleID = roleId, RoleName = "Doctor" };
            _context.Roles.Add(role);

            var existingUser = TestDataFactory.SeedUser(_context, email: "invitee@test.com");
            await _context.SaveChangesAsync();

            var request = new InvitationCreateRequestModel
            {
                HospitalId = hospitalId,
                RoleId = roleId,
                Email = "invitee@test.com",
                Mobile = "9999999999"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already exists"));
        }
    }
}
