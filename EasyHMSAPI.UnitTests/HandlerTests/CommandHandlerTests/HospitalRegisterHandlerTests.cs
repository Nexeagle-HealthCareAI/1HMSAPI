using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HospitalRegisterHandlerTests
    {
        private AppDbContext _context = null!;
        private HospitalRegisterHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new HospitalRegisterHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_RegistersHospital()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User", UserStatusId = 1, EmployeeID = "EMP001" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            var request = new HospitalRegisterRequestModel
            {
                UserId = user.UserID,
                Name = "Grand Hospital",
                Type = "General",
                Email = "info@grand.com",
                Contact = "9876543210",
                Location = "Down Town",
                City = "Metropolis",
                State = "NY",
                Country = "USA",
                Pincode = "10001",
                RegistrationNumber = "REG123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.HospitalId, Is.Not.Null);
            
            var hospital = await _context.Hospitals.FindAsync(response.HospitalId);
            Assert.That(hospital, Is.Not.Null);
            Assert.That(hospital!.Name, Is.EqualTo("Grand Hospital"));

            var hospitalUser = await _context.HospitalUsers.FirstOrDefaultAsync(hu => hu.HospitalID == response.HospitalId);
            Assert.That(hospitalUser, Is.Not.Null);
            Assert.That(hospitalUser!.IsPrimary, Is.True);
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new HospitalRegisterRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }
    }
}
