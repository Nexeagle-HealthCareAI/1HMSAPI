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
    public class DoctorTimeOffListHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorTimeOffListHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorTimeOffListHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsTimeOffs()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();

            var timeOff = new DoctorTimeOff 
            { 
                TimeOffID = Guid.NewGuid(),
                DoctorID = doctor.DoctorID,
                HospitalId = hospitalId,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddDays(1),
                Reason = "Sick Leave"
            };
            _context.DoctorTimeOffs.Add(timeOff);
            await _context.SaveChangesAsync();

            var request = new DoctorTimeOffListRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.TimeOffs, Has.Count.EqualTo(1));
            Assert.That(response.TimeOffs[0].Reason, Is.EqualTo("Sick Leave"));
            Assert.That(response.TimeOffs[0].IsUpcoming, Is.True);
        }
    }
}
