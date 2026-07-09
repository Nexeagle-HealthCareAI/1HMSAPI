using System;
using System.Linq;
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
    public class GetAdmissionReferralsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAdmissionReferralsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAdmissionReferralsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsReferrals_WithPatientAndDoctorNames()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user.UserID, FullName = "Dr Jane Doe", UpdatedAt = DateTime.UtcNow });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123", FullName = "John Smith", Mobile = "9999999999" });

            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123",
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(1));
            Assert.That(response.Referrals[0].PatientName, Is.EqualTo("John Smith"));
            Assert.That(response.Referrals[0].ReferringDoctorName, Is.EqualTo("Dr Jane Doe"));
        }

        [Test]
        public async Task Handle_FilterByStatus_ReturnsOnlyMatching()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT2", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "NOT_ADMITTED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId, StatusCode = "PENDING" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(1));
            Assert.That(response.Referrals[0].PatientId, Is.EqualTo("PAT1"));
        }
    }
}
