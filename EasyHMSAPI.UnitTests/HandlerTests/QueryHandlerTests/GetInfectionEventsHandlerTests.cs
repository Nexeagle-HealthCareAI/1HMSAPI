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
    public class GetInfectionEventsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetInfectionEventsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetInfectionEventsHandler(_context);
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
        public async Task Handle_ReturnsEvents_NewestFirst()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.InfectionEvent.Add(new InfectionEvent
            {
                InfectionEventId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = IpdConstants.InfectionType.Cauti, DiagnosedByDoctorName = "Dr. House", DiagnosedAt = DateTime.UtcNow.AddDays(-2),
            });
            _context.InfectionEvent.Add(new InfectionEvent
            {
                InfectionEventId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = IpdConstants.InfectionType.Vap, DiagnosedByDoctorName = "Dr. House", DiagnosedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetInfectionEventsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Events, Has.Count.EqualTo(2));
            Assert.That(result.Events[0].InfectionType, Is.EqualTo(IpdConstants.InfectionType.Vap));
        }
    }
}
