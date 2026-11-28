#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2: records reach ABDM with no click. HIP_INTI_LINK_505 (a patient with an ABHA is linked once a
    // record is saved) and HIP_INIT_NOTIFY_HIECM (a patient with a mobile and no ABHA gets ABDM's SMS).
    [TestFixture]
    public class AbdmAutoLinkTests
    {
        private AppDbContext _db = null!;
        private Mock<IAbdmHieService> _hie = null!;
        private List<AbdmHieCall> _calls = null!;
        private Guid _hospitalId, _apptId;
        private const string HipId = "IN2810014366";
        private const string PatientId = "P-0001";

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _apptId = Guid.NewGuid();
            _db.Hospitals.Add(new Hospital { HospitalID = _hospitalId, Name = "City Hospital", Type = "Hospital", RegistrationNumber = "R-1", City = "Pune", Contact = "x", Country = "IN", Location = "x", Pincode = "411001", State = "MH" });
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = HipId, UpdatedAt = DateTime.UtcNow });
            _db.Appointments.Add(new Appointment { ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, ApptDate = DateTime.UtcNow.AddHours(-2) });
            _db.SaveChanges();

            _calls = new List<AbdmHieCall>();
            _hie = new Mock<IAbdmHieService>();
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    return new AbdmHieResult { ExchangeId = Guid.NewGuid(), RequestId = Guid.NewGuid().ToString(), HttpStatus = 202 };
                });
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private AbdmAutoLinkService Service() => new(
            _db, new AbdmLinkingService(_db, _hie.Object, NullLogger<AbdmLinkingService>.Instance), _hie.Object,
            NullLogger<AbdmAutoLinkService>.Instance);

        private void Patient(string? abhaId, string? mobile = "9876543210")
        {
            _db.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, FullName = "Asha Rao", Sex = "Female", Mobile = mobile, AbhaId = abhaId });
            if (abhaId != null)
                _db.AbhaAccount.Add(new AbhaAccount { AbhaAccountId = Guid.NewGuid(), HospitalId = _hospitalId, AbhaNumber = abhaId, AbhaAddress = "asha@sbx", FullName = "Asha Rao", Gender = "F", DateOfBirth = "15-08-1990", CreatedAt = DateTime.UtcNow });
        }

        private void Prescription(DateTime updatedAt)
        {
            _db.Prescription.Add(new Prescription { PrescriptionId = Guid.NewGuid(), ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, CreatedAt = updatedAt, UpdatedAt = updatedAt });
            _db.SaveChanges();
        }

        private static string Reference(Guid apptId) => AbdmCareContextRules.ReferenceNumber(AbdmCareContextSource.Opd, apptId);

        [TestCase("91-1234-5678-9012", "9876543210", null, false, AbdmAutoLinkAction.Link)]
        [TestCase("91-1234-5678-9012", "9876543210", AbdmLinkStatus.NotLinked, false, AbdmAutoLinkAction.Link)]
        [TestCase("91-1234-5678-9012", "9876543210", AbdmLinkStatus.Linked, false, AbdmAutoLinkAction.Skip)]
        [TestCase("91-1234-5678-9012", "9876543210", AbdmLinkStatus.TokenRequested, false, AbdmAutoLinkAction.Skip)]
        [TestCase("91-1234-5678-9012", "9876543210", AbdmLinkStatus.Failed, false, AbdmAutoLinkAction.Skip)]
        [TestCase(null, "9876543210", null, false, AbdmAutoLinkAction.Sms)]
        [TestCase("", "+91 98765 43210", null, false, AbdmAutoLinkAction.Sms)]
        [TestCase(null, "9876543210", null, true, AbdmAutoLinkAction.Skip)]
        [TestCase(null, null, null, false, AbdmAutoLinkAction.Skip)]
        [TestCase(null, "12345", null, false, AbdmAutoLinkAction.Skip)]
        public void Decide(string? abhaId, string? mobile, string? status, bool smsSent, AbdmAutoLinkAction expected) =>
            Assert.That(AbdmAutoLinkRules.Decide(abhaId, mobile, status, smsSent), Is.EqualTo(expected));

        [Test]
        public async Task Sweep_PatientWithAbha_NewPrescription_StartsLinkingWithNoClick_505()
        {
            Patient("91-1234-5678-9012");
            Prescription(DateTime.UtcNow.AddMinutes(-1));

            var result = await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(result.LinksStarted, Is.EqualTo(1));
            Assert.That(_calls.Single().Kind, Is.EqualTo("link-token"));
            var careContext = _db.AbdmCareContexts.Single();
            Assert.That(careContext.ReferenceNumber, Is.EqualTo(Reference(_apptId)));
            Assert.That(careContext.UpdatedBy, Is.EqualTo("abdm-auto-link"));
        }

        [Test]
        public async Task Sweep_LinkInFlight_IsNotSentAgain()
        {
            Patient("91-1234-5678-9012");
            Prescription(DateTime.UtcNow.AddMinutes(-1));
            await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);
            _calls.Clear();

            await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public async Task Sweep_PatientWithMobileAndNoAbha_SendsTheRecordSmsOnce_NotifyHiecm()
        {
            Patient(abhaId: null, mobile: "+91 98765 43210");
            Prescription(DateTime.UtcNow.AddMinutes(-1));

            var result = await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(result.SmsSent, Is.EqualTo(1));
            var call = _calls.Single();
            Assert.That(call.Kind, Is.EqualTo(AbdmAutoLinkService.RecordSmsKind));
            Assert.That(call.Path, Is.EqualTo("/api/hiecm/hip/v3/link/patient/links/sms/notify2"));
            Assert.That(call.FacilityHeader, Is.EqualTo(AbdmFacilityHeader.None));
            using var body = JsonDocument.Parse(JsonSerializer.Serialize(call.Body));
            var notification = body.RootElement.GetProperty("notification");
            Assert.That(notification.GetProperty("phoneNo").GetString(), Is.EqualTo("9876543210"));
            Assert.That(notification.GetProperty("hip").GetProperty("id").GetString(), Is.EqualTo(HipId));
            Assert.That(notification.GetProperty("hip").GetProperty("name").GetString(), Is.EqualTo("City Hospital"));

            // The gateway service logs each call; once logged, the next sweep leaves the visit alone.
            _db.AbdmExchanges.Add(new AbdmExchange { ExchangeId = Guid.NewGuid(), RequestId = "r1", HospitalId = _hospitalId, Kind = call.Kind, Path = call.Path, SubjectRef = call.SubjectRef, SentAt = DateTime.UtcNow });
            _db.SaveChanges();
            _calls.Clear();
            await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);
            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public async Task Sweep_OldRecords_AreLeftAlone()
        {
            Patient("91-1234-5678-9012");
            Prescription(DateTime.UtcNow.AddDays(-5));

            var result = await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(result, Is.EqualTo(new AbdmAutoLinkResult(0, 0)));
            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public async Task Sweep_HospitalWithoutAbdmFacility_DoesNothing()
        {
            _db.AbdmFacilities.RemoveRange(_db.AbdmFacilities);
            Patient(abhaId: null);
            Prescription(DateTime.UtcNow.AddMinutes(-1));

            await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(_calls, Is.Empty);
        }
    }
}
