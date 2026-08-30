using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Covers Phase 4's dual-signature requirement: approval now requires a prior technician
    // sign-off and a Doctor record for the approver (a pathologist's sign-off is a medico-legal
    // act, so it must attribute to a real Doctor row, not just any staff User).
    [TestFixture]
    public class ApprovePathologyReportHandlerTests
    {
        private AppDbContext _context = null!;
        private ApprovePathologyReportHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ApprovePathologyReportHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private PathologyReport SeedTechSignedReport(Guid hospitalId)
        {
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-1",
                Status = "TECH_SIGNED",
                TechnicianName = "Ravi Technician",
                TechnicianRegNo = "DMLT-12345",
                TechnicianSignedAt = DateTime.UtcNow,
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();
            return report;
        }

        private Doctor SeedDoctor(Guid userId)
        {
            var doctor = new Doctor
            {
                DoctorID = Guid.NewGuid(),
                UserID = userId,
                LicenseNumber = "MCI-99999",
            };
            _context.Doctors.Add(doctor);
            _context.SaveChanges();
            return doctor;
        }

        [Test]
        public async Task Handle_TechSignedReportWithRegisteredDoctor_ApprovesAndCapturesPathologistIdentity()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedTechSignedReport(hospitalId);
            var userId = Guid.NewGuid();
            var doctor = SeedDoctor(userId);

            var result = await _handler.Handle(new ApprovePathologyReportCommand
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                PathologistRegNo = "MCI-99999",
                LoggedInUserId = userId,
                LoggedInUserName = "Dr. Asha Rao",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyReport.Single(r => r.ReportId == report.ReportId);
            Assert.That(saved.Status, Is.EqualTo("APPROVED"));
            Assert.That(saved.PathologistDoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(saved.PathologistName, Is.EqualTo("Dr. Asha Rao"));
            Assert.That(saved.PathologistRegNo, Is.EqualTo("MCI-99999"));
            Assert.That(saved.ApprovedAt, Is.Not.Null);
        }

        [Test]
        public void Handle_DraftReportNotYetTechSigned_Throws()
        {
            var hospitalId = Guid.NewGuid();
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-2",
                Status = "DRAFT",
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();
            var userId = Guid.NewGuid();
            SeedDoctor(userId);

            var ex = Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new ApprovePathologyReportCommand
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                PathologistRegNo = "MCI-99999",
                LoggedInUserId = userId,
            }, CancellationToken.None));
            Assert.That(ex!.Message, Does.Contain("technician"));
        }

        [Test]
        public void Handle_ApproverHasNoDoctorRecord_Throws()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedTechSignedReport(hospitalId);

            var ex = Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new ApprovePathologyReportCommand
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                PathologistRegNo = "MCI-99999",
                LoggedInUserId = Guid.NewGuid(), // no Doctor row for this user
            }, CancellationToken.None));
            Assert.That(ex!.Message, Does.Contain("registered doctor"));
        }

        [Test]
        public void Handle_AlreadyApprovedReport_Throws()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedTechSignedReport(hospitalId);
            report.Status = "APPROVED";
            _context.SaveChanges();
            var userId = Guid.NewGuid();
            SeedDoctor(userId);

            Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new ApprovePathologyReportCommand
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                PathologistRegNo = "MCI-99999",
                LoggedInUserId = userId,
            }, CancellationToken.None));
        }

        [Test]
        public void Handle_MissingPathologistRegNo_Throws()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedTechSignedReport(hospitalId);
            var userId = Guid.NewGuid();
            SeedDoctor(userId);

            Assert.ThrowsAsync<ApplicationException>(() => _handler.Handle(new ApprovePathologyReportCommand
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                PathologistRegNo = "",
                LoggedInUserId = userId,
            }, CancellationToken.None));
        }
    }
}
