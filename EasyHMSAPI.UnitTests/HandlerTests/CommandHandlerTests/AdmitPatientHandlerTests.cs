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
    public class AdmitPatientHandlerTests
    {
        private AppDbContext _context = null!;
        private AdmitPatientHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmitPatientHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid hospitalId, Guid doctorId, string patientId) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var patientId = "PTID00000001";
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = patientId,
                FullName = "Test Patient", RegisteredAt = DateTime.UtcNow, Country = "India",
            });
            _context.SaveChanges();
            return (hospitalId, doctor.DoctorID, patientId);
        }

        [Test]
        public async Task Handle_ValidRequest_AdmitsExistingPatient()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", EnableIpdBilling = false, LoggedInUserName = "Front Desk",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.AdmissionId, Is.Not.Null);
            Assert.That(response.WasExisting, Is.True);
        }

        [Test]
        public async Task Handle_PrimaryDoctorIdSupplied_SeedsInitialAdmissionDoctorAssignmentRow()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", EnableIpdBilling = false, LoggedInUserName = "Front Desk",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var rows = _context.AdmissionDoctorAssignment.Where(a => a.AdmissionId == response.AdmissionId).ToList();
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].DoctorId, Is.EqualTo(doctorId));
            Assert.That(rows[0].StatusCode, Is.EqualTo("ACTIVE"));
            Assert.That(rows[0].UnassignedAt, Is.Null);
        }

        [Test]
        public async Task Handle_OtPlanIdSupplied_SnapshotsProcedureAndIcuLevel_AndDefaultsRoomCategory()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var plan = new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId,
                PlanName = "PCNL Plan", ProcedureName = "Percutaneous Nephrolithotomy",
                DefaultRoomCategory = "SEMI_PRIVATE", SuggestedIcuLevel = "LEVEL_2",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.OTPlans.Add(plan);
            await _context.SaveChangesAsync();

            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", OtPlanId = plan.OtPlanId, EnableIpdBilling = false,
                LoggedInUserName = "Front Desk",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var admission = await _context.Admission.FindAsync(response.AdmissionId);
            Assert.That(admission!.OtPlanId, Is.EqualTo(plan.OtPlanId));
            Assert.That(admission.OtPlanProcedureNameSnapshot, Is.EqualTo("Percutaneous Nephrolithotomy"));
            Assert.That(admission.OtPlanSuggestedIcuLevel, Is.EqualTo("LEVEL_2"));

            var coverage = _context.AdmissionCoverage.FirstOrDefault(c => c.AdmissionId == admission.AdmissionId);
            Assert.That(coverage, Is.Not.Null);
            Assert.That(coverage!.EntitledRoomCategory, Is.EqualTo("SEMI_PRIVATE"));
        }

        [Test]
        public async Task Handle_ExplicitEntitledRoomCategory_OverridesOtPlanDefault()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var plan = new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId,
                PlanName = "PCNL Plan", ProcedureName = "PCNL", DefaultRoomCategory = "SEMI_PRIVATE",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.OTPlans.Add(plan);
            await _context.SaveChangesAsync();

            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", OtPlanId = plan.OtPlanId, EntitledRoomCategory = "PRIVATE",
                EnableIpdBilling = false,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var coverage = _context.AdmissionCoverage.FirstOrDefault(c => c.AdmissionId == response.AdmissionId);
            Assert.That(coverage!.EntitledRoomCategory, Is.EqualTo("PRIVATE"));
        }

        [Test]
        public async Task Handle_ReferralIdSupplied_MarksReferralConverted_AndLinksAdmission()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var referral = new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = patientId,
                ReferringDoctorId = doctorId, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionReferrals.Add(referral);
            await _context.SaveChangesAsync();

            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", ReferralId = referral.ReferralId, EnableIpdBilling = false,
                LoggedInUserName = "Front Desk",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updatedReferral = await _context.AdmissionReferrals.FindAsync(referral.ReferralId);
            Assert.That(updatedReferral!.StatusCode, Is.EqualTo("CONVERTED"));
            Assert.That(updatedReferral.ConvertedAdmissionId, Is.EqualTo(response.AdmissionId));

            var history = _context.AdmissionReferralStatusHistories.Where(h => h.ReferralId == referral.ReferralId).ToList();
            Assert.That(history.Any(h => h.StatusCode == "CONVERTED"), Is.True);
        }

        [Test]
        public async Task Handle_AlreadyConvertedReferral_LeftUnchanged_AdmissionStillSucceeds()
        {
            var (hospitalId, doctorId, patientId) = SeedBasics();
            var otherAdmissionId = Guid.NewGuid();
            var referral = new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = patientId,
                ReferringDoctorId = doctorId, CaseType = "PLANNED", StatusCode = "CONVERTED",
                ConvertedAdmissionId = otherAdmissionId,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionReferrals.Add(referral);
            await _context.SaveChangesAsync();

            var request = new AdmitPatientRequestModel
            {
                HospitalId = hospitalId, PatientId = patientId, PrimaryDoctorId = doctorId,
                AdmissionType = "ELECTIVE", ReferralId = referral.ReferralId, EnableIpdBilling = false,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var unchangedReferral = await _context.AdmissionReferrals.FindAsync(referral.ReferralId);
            Assert.That(unchangedReferral!.ConvertedAdmissionId, Is.EqualTo(otherAdmissionId));
        }
    }
}
