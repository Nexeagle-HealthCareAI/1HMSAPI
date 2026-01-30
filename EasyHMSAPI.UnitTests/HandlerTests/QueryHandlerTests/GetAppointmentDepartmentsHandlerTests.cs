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
    public class GetAppointmentDepartmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAppointmentDepartmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAppointmentDepartmentsHandler(_context);
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
            var dept = new Department { DepartmentID = Guid.NewGuid(), Name = "Derma" };
            _context.Departments.Add(dept);

            var mapping = new HospitalDepartmentMapping 
            { 
                MappingID = Guid.NewGuid(), 
                HospitalID = hospitalId, 
                DepartmentID = dept.DepartmentID, 
                IsActive = true 
            };
            _context.HospitalDepartmentMappings.Add(mapping);
            await _context.SaveChangesAsync();

            var request = new GetAppointmentDepartmentsRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Departments, Has.Count.EqualTo(1));
            Assert.That(response.Departments[0].DepartmentName, Is.EqualTo("Derma"));
        }
    }
}
