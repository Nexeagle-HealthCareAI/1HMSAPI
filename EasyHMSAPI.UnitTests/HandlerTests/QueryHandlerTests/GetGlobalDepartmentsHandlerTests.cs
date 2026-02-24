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
    public class GetGlobalDepartmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetGlobalDepartmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetGlobalDepartmentsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsGlobalDepartmentsOnly()
        {
            // Arrange
            var globalDept = new Department { DepartmentID = Guid.NewGuid(), Name = "Global Dept", HospitalID = null };
            var hospitalDept = new Department { DepartmentID = Guid.NewGuid(), Name = "Local Dept", HospitalID = Guid.NewGuid() };
            
            _context.Departments.Add(globalDept);
            _context.Departments.Add(hospitalDept);
            await _context.SaveChangesAsync();

            var request = new GetGlobalDepartmentsRequestModel();

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Departments, Has.Count.EqualTo(1));
            Assert.That(response.Departments[0].Name, Is.EqualTo("Global Dept"));
        }
    }
}
