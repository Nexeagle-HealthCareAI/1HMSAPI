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
    public class InvitationListHandlerTests
    {
        private AppDbContext _context = null!;
        private InvitationListHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new InvitationListHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsInvitations()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            // Seed dependencies like Role
            _context.Roles.Add(new Role { RoleID = roleId, RoleName = "Test Role" });

            var invite = new UserInvitation
            {
                InvitationID = Guid.NewGuid(),
                HospitalID = hospitalId,
                InvitedByUserID = Guid.NewGuid(),
                RoleID = roleId,
                RecipientName = "Recipient",
                RecipientMobile = "1234567890",
                Status = "Pending",
                TokenHash = new byte[32],
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _context.UserInvitations.Add(invite);
            await _context.SaveChangesAsync();

            var request = new InvitationListRequestModel { HospitalId = hospitalId, Scope = "Pending" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Invitations, Has.Count.EqualTo(1));
            Assert.That(response.Invitations[0].RecipientName, Is.EqualTo("Recipient"));
        }
    }
}
