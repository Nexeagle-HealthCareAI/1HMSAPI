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
    public class GetHospitalUsersHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalUsersHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalUsersHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsUser()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var hospitalId = Guid.NewGuid();
            var hospitalUser = new HospitalUser
            {
                HospitalUserID = Guid.NewGuid(),
                HospitalID = hospitalId,
                UserID = user.UserID,
                IsPrimary = true
            };
            _context.HospitalUsers.Add(hospitalUser);
            await _context.SaveChangesAsync();

            var request = new GetHospitalUsersRequestModel(user.UserID);

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.UserId, Is.EqualTo(user.UserID));
            Assert.That(response.IsPrimary, Is.EqualTo("True"));
        }

        [Test]
        public async Task Handle_NotFound_ReturnsNull()
        {
             // Arrange
            var request = new GetHospitalUsersRequestModel(Guid.NewGuid());

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Null);
        }
    }
}
