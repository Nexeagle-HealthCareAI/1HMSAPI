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
    public class DoctorSlotsHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorSlotsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorSlotsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_ReturnsSlots()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();

             var shiftTemplate = new DoctorShiftTemplate { TemplateID = Guid.NewGuid(), ShiftName = "Morning", IsActive = true };
            _context.DoctorShiftTemplates.Add(shiftTemplate);
            await _context.SaveChangesAsync();

            var request = new DoctorSlotsRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                SlotDate = DateTime.Today
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.ShiftInfo, Is.Not.Null);
            Assert.That(response.ShiftInfo[0].DataSource, Is.EqualTo(AppConstants.ShiftDataSource_Default));
        }

        [Test]
        public async Task Handle_TimeOff_ReturnsTimeOff()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var date = DateTime.Today;

            var timeOff = new DoctorTimeOff 
            { 
                TimeOffID = Guid.NewGuid(),
                DoctorID = doctor.DoctorID,
                HospitalId = hospitalId,
                FromDate = date,
                ToDate = date,
                Reason = "Vacation"
            };
            _context.DoctorTimeOffs.Add(timeOff);
            await _context.SaveChangesAsync();

            var request = new DoctorSlotsRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                SlotDate = date
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.IsTimeOff, Is.True);
            Assert.That(response.TimeOffReason, Is.EqualTo("Vacation"));
            Assert.That(response.ShiftInfo, Is.Null);
        }
    }
}
