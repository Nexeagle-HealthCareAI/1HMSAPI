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
    public class DoctorSpecializationsHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorSpecializationsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorSpecializationsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsSpecializations()
        {
            // Arrange
            var deptId = Guid.NewGuid();
            var spec = new Specialization { SpecializationID = Guid.NewGuid(), DepartmentID = deptId, Name = "Cardiology", IsActive = true };
            _context.Specializations.Add(spec);
            await _context.SaveChangesAsync();

            var request = new DoctorSpecializationsRequestModel
            {
                DepartmentId = deptId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].Name, Is.EqualTo("Cardiology"));
        }
    }
}
