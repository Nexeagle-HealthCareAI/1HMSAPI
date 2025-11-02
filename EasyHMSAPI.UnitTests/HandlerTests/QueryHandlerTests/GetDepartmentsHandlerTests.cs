using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDepartmentsHandlerTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        [Test]
        public async Task Handle_Returns_Global_And_Matching_Hospital_Departments()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var globalDept = new Department { DepartmentID = Guid.NewGuid(), HospitalID = null, Name = "Global", IsActive = true };
            var matchDept = new Department { DepartmentID = Guid.NewGuid(), HospitalID = hospitalId, Name = "Match", IsActive = true };
            var otherDept = new Department { DepartmentID = Guid.NewGuid(), HospitalID = Guid.NewGuid(), Name = "Other", IsActive = true };

            _context.Departments.AddRange(globalDept, matchDept, otherDept);
            await _context.SaveChangesAsync();

            var handler = new GetDepartmentsHandler(_context);
            var request = new GetDepartmentsRequestModel { HospitalId = hospitalId };

            // Act
            var response = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Departments, Is.Not.Null);
            var resultIds = response.Departments.Select(d => d.DepartmentID).ToList();
            Assert.That(resultIds, Does.Contain(globalDept.DepartmentID));
            Assert.That(resultIds, Does.Contain(matchDept.DepartmentID));
            Assert.That(resultIds, Does.Not.Contain(otherDept.DepartmentID));
        }
    }
}
