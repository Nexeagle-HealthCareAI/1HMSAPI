using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class CreateDepartmentHandlerTests
    {
        private AppDbContext _context = null!;
        private CreateDepartmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CreateDepartmentHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesDepartment()
        {
            // Arrange
            var request = new CreateDepartmentRequestModel
            {
                HospitalID = Guid.NewGuid(),
                Name = "Cardiology",
                Description = "Heart stuff",
                CreatedByUserID = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Message, Does.Contain("created successfully"));
            Assert.That(response.DepartmentID, Is.Not.EqualTo(Guid.Empty));
            
            var dept = await _context.Departments.FindAsync(response.DepartmentID);
            Assert.That(dept, Is.Not.Null);
            Assert.That(dept!.Name, Is.EqualTo("Cardiology"));
            
            var mapping = await _context.HospitalDepartmentMappings.FirstOrDefaultAsync(m => m.DepartmentID == response.DepartmentID);
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.HospitalID, Is.EqualTo(request.HospitalID));
        }
    }
}
