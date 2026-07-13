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
    public class GetPublicDoctorAvailabilityHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicDoctorAvailabilityHandler _handler = null!;
        private Guid _hospitalId;
        private Doctor _doctor = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicDoctorAvailabilityHandler(_context);

            _hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            _doctor = TestDataFactory.SeedDoctor(_context, user);
            _doctor.HospitalId = _hospitalId;
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorFromDifferentHospital_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicDoctorAvailabilityRequestModel
            {
                HospitalId = Guid.NewGuid(),
                DoctorId = _doctor.DoctorID,
                Date = DateTime.Today,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_DoctorOnTimeOff_ReturnsUnavailableWithReason()
        {
            var targetDate = DateTime.Today.AddDays(2);
            _context.DoctorTimeOffs.Add(new DoctorTimeOff
            {
                TimeOffID = Guid.NewGuid(),
                DoctorID = _doctor.DoctorID,
                HospitalId = _hospitalId,
                FromDate = targetDate,
                ToDate = targetDate,
                Reason = "On leave",
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorAvailabilityRequestModel
            {
                HospitalId = _hospitalId,
                DoctorId = _doctor.DoctorID,
                Date = targetDate,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsAvailable, Is.False);
            Assert.That(response.Reason, Is.EqualTo("On leave"));
        }

        [Test]
        public async Task Handle_OverrideShiftExists_ReturnsOverrideShiftsAsAvailable()
        {
            var targetDate = DateTime.Today.AddDays(3);
            _context.DoctorShiftOverrides.Add(new DoctorShiftOverride
            {
                OverrideID = Guid.NewGuid(),
                DoctorID = _doctor.DoctorID,
                HospitalId = _hospitalId,
                ShiftName = "Evening Special",
                StartTime = new TimeSpan(17, 0, 0),
                EndTime = new TimeSpan(20, 0, 0),
                StartDate = targetDate,
                EndDate = targetDate,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicDoctorAvailabilityRequestModel
            {
                HospitalId = _hospitalId,
                DoctorId = _doctor.DoctorID,
                Date = targetDate,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsAvailable, Is.True);
            Assert.That(response.Shifts, Has.Count.EqualTo(1));
            Assert.That(response.Shifts[0].Name, Is.EqualTo("Evening Special"));
        }

        [Test]
        public async Task Handle_NoOverrideOrTemplate_ReturnsUnavailable()
        {
            var response = await _handler.Handle(new GetPublicDoctorAvailabilityRequestModel
            {
                HospitalId = _hospitalId,
                DoctorId = _doctor.DoctorID,
                Date = DateTime.Today.AddDays(1),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsAvailable, Is.False);
            Assert.That(response.Shifts, Is.Empty);
        }
    }
}
