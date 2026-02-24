using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorShiftConfigHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorShiftConfigHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorShiftConfigHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsShiftConfig()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();

            var shiftTemplate = new DoctorShiftTemplate { TemplateID = Guid.NewGuid(), ShiftName = "Morning", IsActive = true };
            _context.DoctorShiftTemplates.Add(shiftTemplate);
            await _context.SaveChangesAsync();

            var request = new DoctorShiftConfigRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                StartDate = DateTime.Today,
                DaysCount = 1
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.ShiftInfo, Has.Count.EqualTo(1));
            Assert.That(response.ShiftInfo[0].DataSource, Is.EqualTo(AppConstants.ShiftDataSource_Default));
            Assert.That(response.ShiftInfo[0].ShiftDayDetails, Has.Count.EqualTo(1));
            Assert.That(response.ShiftInfo[0].ShiftDayDetails[0].ShiftName, Is.EqualTo("Morning"));
        }

         [Test]
        public async Task Handle_WithOverride_ReturnsOverrideConfig()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var date = DateTime.Today;

             var overrideShift = new DoctorShiftOverride 
             { 
                 OverrideID = Guid.NewGuid(), 
                 DoctorID = doctor.DoctorID, 
                 HospitalId = hospitalId,
                 StartDate = date,
                 EndDate = date,
                 ShiftName = "Override"
             };
            _context.DoctorShiftOverrides.Add(overrideShift);
            await _context.SaveChangesAsync();

            var request = new DoctorShiftConfigRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                StartDate = date,
                DaysCount = 1
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.ShiftInfo[0].DataSource, Is.EqualTo(AppConstants.ShiftDataSource_Override));
            Assert.That(response.ShiftInfo[0].ShiftDayDetails[0].ShiftName, Is.EqualTo("Override"));
        }
    }
}
