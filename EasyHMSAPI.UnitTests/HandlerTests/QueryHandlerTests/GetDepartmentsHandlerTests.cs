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
    public class GetDepartmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDepartmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDepartmentsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsDepartments()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var globalDept = new Department { DepartmentID = Guid.NewGuid(), Name = "Global", HospitalID = null, IsActive = true };
            var hospitalDept = new Department { DepartmentID = Guid.NewGuid(), Name = "Local", HospitalID = hospitalId, IsActive = true };
            
            _context.Departments.Add(globalDept);
            _context.Departments.Add(hospitalDept);
            await _context.SaveChangesAsync();

            var request = new GetDepartmentsRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Departments, Has.Count.EqualTo(2));
            Assert.That(response.Departments, Has.Some.Property("Name").EqualTo("Global"));
            Assert.That(response.Departments, Has.Some.Property("Name").EqualTo("Local"));
        }
    }
}
