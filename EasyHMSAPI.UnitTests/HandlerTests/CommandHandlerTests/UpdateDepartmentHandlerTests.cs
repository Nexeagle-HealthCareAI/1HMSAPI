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
    public class UpdateDepartmentHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateDepartmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateDepartmentHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesDepartment()
        {
            // Arrange
            var deptId = Guid.NewGuid();
            var department = new Department { DepartmentID = deptId, Name = "Old Name", Description = "Old Desc" };
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            var request = new UpdateDepartmentRequestModel
            {
                DepartmentId = deptId,
                Name = "New Name",
                Description = "New Desc"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updated = await _context.Departments.FindAsync(deptId);
            Assert.That(updated!.Name, Is.EqualTo("New Name"));
            Assert.That(updated.Description, Is.EqualTo("New Desc"));
        }

        [Test]
        public async Task Handle_DepartmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UpdateDepartmentRequestModel { DepartmentId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Department not found."));
        }
    }
}
