using System;
using System.Linq;
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
    public class GetInfectionRateSummaryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetInfectionRateSummaryHandler _handler = null!;

        private static readonly DateTime RangeStart = new DateTime(2026, 1, 1);
        private static readonly DateTime RangeEnd = new DateTime(2026, 1, 11); // 10-day window

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetInfectionRateSummaryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        [Test]
        public async Task Handle_ComputesRatePer1000DeviceDays_ForCentralLineClabsi()
        {
            var (hospitalId, admission) = SeedBasics();
            // 10 device-days for a central line fully inside the range.
            _context.DeviceAssignment.Add(new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
                InsertedAt = RangeStart, RemovedAt = RangeEnd, StatusCode = IpdConstants.DeviceStatus.Removed,
            });
            _context.InfectionEvent.Add(new InfectionEvent
            {
                InfectionEventId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = IpdConstants.InfectionType.Clabsi, DiagnosedByDoctorName = "Dr. House", DiagnosedAt = RangeStart.AddDays(1),
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetInfectionRateSummaryRequestModel { HospitalId = hospitalId, FromDate = RangeStart, ToDate = RangeEnd }, CancellationToken.None);

            Assert.That(result.Success, Is.True, result.Message);
            var clabsi = result.Rates.Single(r => r.InfectionType == IpdConstants.InfectionType.Clabsi);
            Assert.That(clabsi.DeviceDays, Is.EqualTo(10m));
            Assert.That(clabsi.InfectionCount, Is.EqualTo(1));
            Assert.That(clabsi.RatePer1000DeviceDays, Is.EqualTo(100m));
        }

        [Test]
        public async Task Handle_NoDeviceDays_RateIsNull()
        {
            var (hospitalId, _) = SeedBasics();

            var result = await _handler.Handle(new GetInfectionRateSummaryRequestModel { HospitalId = hospitalId, FromDate = RangeStart, ToDate = RangeEnd }, CancellationToken.None);

            Assert.That(result.Rates, Has.Count.EqualTo(3));
            Assert.That(result.Rates.All(r => r.RatePer1000DeviceDays == null), Is.True);
        }

        [Test]
        public async Task Handle_InfectionOutsideRange_ExcludedFromCount()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.DeviceAssignment.Add(new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.Ett, InsertedByDoctorName = "Dr. House",
                InsertedAt = RangeStart, RemovedAt = RangeEnd, StatusCode = IpdConstants.DeviceStatus.Removed,
            });
            _context.InfectionEvent.Add(new InfectionEvent
            {
                InfectionEventId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = IpdConstants.InfectionType.Vap, DiagnosedByDoctorName = "Dr. House", DiagnosedAt = RangeStart.AddMonths(-1),
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetInfectionRateSummaryRequestModel { HospitalId = hospitalId, FromDate = RangeStart, ToDate = RangeEnd }, CancellationToken.None);

            var vap = result.Rates.Single(r => r.InfectionType == IpdConstants.InfectionType.Vap);
            Assert.That(vap.InfectionCount, Is.EqualTo(0));
        }
    }
}
