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
    public class GetRapidResponseHandlerTests
    {
        private AppDbContext _context = null!;
        private GetRapidResponseHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetRapidResponseHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics(string patientId = "PTID00000001")
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
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = patientId,
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        [Test]
        public async Task Handle_History_ReturnsResponseTimeSeconds_WhenArrived()
        {
            var (hospitalId, admission) = SeedBasics();
            var calledAt = DateTime.UtcNow.AddMinutes(-10);
            _context.RapidResponseActivation.Add(new RapidResponseActivation
            {
                ActivationId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                TriggerReason = "HIGH_EWS", CalledBy = "Nurse Joy", CalledAt = calledAt, ArrivedAt = calledAt.AddMinutes(4),
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetRapidResponseHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Activations, Has.Count.EqualTo(1));
            Assert.That(result.Activations[0].ResponseTimeSeconds, Is.EqualTo(240));
        }

        [Test]
        public async Task Handle_Open_OnlyReturnsUnresolvedActivations_WithPatientName()
        {
            var (hospitalId, admission) = SeedBasics(patientId: "PTID00000002");
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                PatientId = "PTID00000002", HospitalId = hospitalId, FullName = "Jane Doe", RegistrationId = Guid.NewGuid(), RegisteredAt = DateTime.UtcNow,
            });
            _context.RapidResponseActivation.Add(new RapidResponseActivation
            {
                ActivationId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId, PatientId = "PTID00000002",
                TriggerReason = "NURSE_CONCERN", CalledBy = "Nurse Joy", CalledAt = DateTime.UtcNow,
            });
            _context.RapidResponseActivation.Add(new RapidResponseActivation
            {
                ActivationId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId, PatientId = "PTID00000002",
                TriggerReason = "OTHER", CalledBy = "Nurse Joy", CalledAt = DateTime.UtcNow.AddHours(-1), ResolvedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetOpenRapidResponsesRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(result.Activations, Has.Count.EqualTo(1));
            Assert.That(result.Activations[0].PatientName, Is.EqualTo("Jane Doe"));
        }
    }
}
