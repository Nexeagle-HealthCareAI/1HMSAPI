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
    [TestFixture]
    public class AdviseAdmissionHandlerTests
    {
        private AppDbContext _context = null!;
        private AdviseAdmissionHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdviseAdmissionHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesReferral_WithPendingStatus_AndHistory()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var request = new AdviseAdmissionRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PAT123",
                ReferringDoctorId = doctor.DoctorID,
                ProcedureName = "PCNL",
                CaseType = "planned",
                LoggedInUserName = "Dr Test",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ReferralId, Is.Not.Null);

            var saved = await _context.AdmissionReferrals.FindAsync(response.ReferralId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.StatusCode, Is.EqualTo("PENDING"));
            Assert.That(saved.CaseType, Is.EqualTo("PLANNED"));

            var history = _context.AdmissionReferralStatusHistories.Where(h => h.ReferralId == saved.ReferralId).ToList();
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].StatusCode, Is.EqualTo("PENDING"));
        }

        [Test]
        public async Task Handle_OtPlanSupplied_AutoFillsProcedureName()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var plan = new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId,
                PlanName = "PCNL Plan", ProcedureName = "Percutaneous Nephrolithotomy",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.OTPlans.Add(plan);
            await _context.SaveChangesAsync();

            var request = new AdviseAdmissionRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PAT123",
                ReferringDoctorId = doctor.DoctorID,
                OtPlanId = plan.OtPlanId,
                CaseType = "PLANNED",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = await _context.AdmissionReferrals.FindAsync(response.ReferralId);
            Assert.That(saved!.ProcedureName, Is.EqualTo("Percutaneous Nephrolithotomy"));
        }

        [Test]
        public async Task Handle_PackageTypeId_PersistsOnReferral_IndependentOfOtPlan()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var packageType = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Full Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.PackageTypes.Add(packageType);
            await _context.SaveChangesAsync();

            var request = new AdviseAdmissionRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PAT123",
                ReferringDoctorId = doctor.DoctorID,
                PackageTypeId = packageType.PackageTypeId,
                ProcedureName = "PCNL",
                CaseType = "PLANNED",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = await _context.AdmissionReferrals.FindAsync(response.ReferralId);
            Assert.That(saved!.PackageTypeId, Is.EqualTo(packageType.PackageTypeId));
            Assert.That(saved.OtPlanId, Is.Null);
        }

        [Test]
        public async Task Handle_InvalidCaseType_ReturnsError()
        {
            var request = new AdviseAdmissionRequestModel
            {
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123",
                ReferringDoctorId = Guid.NewGuid(),
                CaseType = "SOMEDAY",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("EMERGENCY, PLANNED, URGENT"));
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            var request = new AdviseAdmissionRequestModel
            {
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123",
                ReferringDoctorId = Guid.NewGuid(),
                CaseType = "EMERGENCY",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("doctor not found"));
        }
    }
}
