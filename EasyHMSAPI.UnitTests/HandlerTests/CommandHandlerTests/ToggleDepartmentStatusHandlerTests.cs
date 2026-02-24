using System;
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
    public class ToggleDepartmentStatusHandlerTests
    {
        private AppDbContext _context = null!;
        private ToggleDepartmentStatusHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ToggleDepartmentStatusHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_TogglesStatus()
        {
            // Arrange
            var deptId = Guid.NewGuid();
            var department = new Department { DepartmentID = deptId, Name = "Dept", IsActive = true };
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            var request = new ToggleDepartmentStatusRequestModel { DepartmentId = deptId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.IsActive, Is.False);
            
            // Toggle back
            var response2 = await _handler.Handle(request, CancellationToken.None);
            Assert.That(response2.IsActive, Is.True);
        }

        [Test]
        public async Task Handle_DepartmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new ToggleDepartmentStatusRequestModel { DepartmentId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Message, Is.EqualTo("Department not found."));
            Assert.That(response.IsActive, Is.False);
        }
    }
}
