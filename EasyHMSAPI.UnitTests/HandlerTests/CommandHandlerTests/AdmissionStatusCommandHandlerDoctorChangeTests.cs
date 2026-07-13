using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Regression coverage for the AdmissionDoctorAssignmentHelper wiring inside
    // AdmissionStatusCommandHandlers.Handle(UpdateAdmissionDetailsRequestModel) — the existing
    // "Edit admission details" path must produce the same audit trail as the dedicated
    // ChangeAdmittingDoctor command, not silently bypass it (see AdmissionDoctorAssignmentHelper).
    [TestFixture]
    public class AdmissionStatusCommandHandlerDoctorChangeTests
    {
        private AppDbContext _context = null!;
        private AdmissionStatusCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmissionStatusCommandHandlers(
                _context, new Mock<ISmsService>().Object, new Mock<IWhatsAppMessagingService>().Object, new Mock<IMediator>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_NewPrimaryDoctorId_WritesAdmissionDoctorAssignmentRow_NotJustOverwritesField()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            _context.SaveChanges();

            var oldDoctorId = Guid.NewGuid();
            var newDoctorUser = TestDataFactory.SeedUser(_context, email: "newdoc@example.com", phone: "4444444444");
            var newDoctor = TestDataFactory.SeedDoctor(_context, newDoctorUser);

            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, PrimaryDoctorId = oldDoctorId,
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();

            var response = await _handler.Handle(new UpdateAdmissionDetailsRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, PrimaryDoctorId = newDoctor.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);

            var updatedAdmission = await _context.Admission.FindAsync(admission.AdmissionId);
            Assert.That(updatedAdmission!.PrimaryDoctorId, Is.EqualTo(newDoctor.DoctorID));

            var rows = _context.AdmissionDoctorAssignment.Where(a => a.AdmissionId == admission.AdmissionId).ToList();
            Assert.That(rows, Has.Count.EqualTo(1), "Expected exactly one history row (no prior ACTIVE row existed to release).");
            Assert.That(rows[0].DoctorId, Is.EqualTo(newDoctor.DoctorID));
            Assert.That(rows[0].StatusCode, Is.EqualTo("ACTIVE"));
        }
    }
}
