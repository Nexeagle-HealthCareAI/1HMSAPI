using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Hospital registration creates the admin-doctor's profile with the placeholder licence "PENDING". Such a doctor has to confirm the profile
    // (real licence, state medical council, year of registration) before they can go online; until then the Doctor Board asks them to.
    [TestFixture]
    public class DoctorProfileConfirmationTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // ---------------------------------------------------------------- the rules

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("PENDING")]
        [TestCase("pending")]
        [TestCase("  Pending ")]
        public void Placeholder_Licences_AreNotRealLicences(string? licence) => Assert.That(DoctorProfileRules.IsPlaceholderLicence(licence), Is.True);

        [TestCase("KMC-12345")]
        [TestCase("12345")]
        public void RealLicences_AreAccepted(string licence) => Assert.That(DoctorProfileRules.IsPlaceholderLicence(licence), Is.False);

        [Test]
        public void ARegisteredAdminDoctor_IsMissingAllThree()
        {
            var missing = DoctorProfileRules.MissingItems("PENDING", null, null);
            Assert.That(missing, Has.Count.EqualTo(3));
            Assert.That(missing[0], Does.Contain("licence"));
            Assert.That(missing[1], Does.Contain("council"));
            Assert.That(missing[2], Does.Contain("Year"));
        }

        [Test]
        public void ARealProfile_IsConfirmed() =>
            Assert.That(DoctorProfileRules.MissingItems("KMC-1", "Karnataka Medical Council", 2012), Is.Empty);

        [TestCase(1949)]
        [TestCase(3000)]
        [TestCase(null)]
        public void ImplausibleRegistrationYears_AreNotConfirmed(int? year) =>
            Assert.That(DoctorProfileRules.MissingItems("KMC-1", "KMC", year), Has.Count.EqualTo(1));

        // ---------------------------------------------------------------- going online

        private (User User, Doctor Doctor, Hospital Hospital) SeedDoctor(string licence = "KMC-1", string? council = "KMC", int? year = 2012)
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            doctor.LicenseNumber = licence;
            doctor.MedicalCouncil = council;
            doctor.RegistrationYear = year;
            doctor.IsOnlineNow = false;
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            _context.SaveChanges();
            return (user, doctor, hospital);
        }

        private bool IsOnline(Doctor d) => _context.Doctors.AsNoTracking().First(x => x.DoctorID == d.DoctorID).IsOnlineNow;

        [Test]
        public async Task Self_UnconfirmedDoctor_CannotGoOnline()
        {
            var (user, doctor, _) = SeedDoctor(licence: "PENDING", council: null, year: null);

            var response = await new UpdateOwnOnlineStatusHandler(_context).Handle(new UpdateOwnOnlineStatusRequestModel { CallerUserId = user.UserID, IsOnlineNow = true }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Confirm your professional profile"));
            Assert.That(IsOnline(doctor), Is.False);
        }

        [Test]
        public async Task Self_ConfirmedDoctor_CanGoOnline_AndOffline()
        {
            var (user, doctor, _) = SeedDoctor();
            var handler = new UpdateOwnOnlineStatusHandler(_context);

            Assert.That((await handler.Handle(new UpdateOwnOnlineStatusRequestModel { CallerUserId = user.UserID, IsOnlineNow = true }, CancellationToken.None)).Success, Is.True);
            Assert.That(IsOnline(doctor), Is.True);
            Assert.That((await handler.Handle(new UpdateOwnOnlineStatusRequestModel { CallerUserId = user.UserID, IsOnlineNow = false }, CancellationToken.None)).Success, Is.True);
            Assert.That(IsOnline(doctor), Is.False);
        }

        [Test]
        public async Task Self_UnconfirmedDoctor_CanStillTurnOffline()
        {
            var (user, doctor, _) = SeedDoctor(licence: "PENDING", council: null, year: null);
            doctor.IsOnlineNow = true;   // e.g. was online before the rule existed
            _context.SaveChanges();

            var response = await new UpdateOwnOnlineStatusHandler(_context).Handle(new UpdateOwnOnlineStatusRequestModel { CallerUserId = user.UserID, IsOnlineNow = false }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(IsOnline(doctor), Is.False);
        }

        [TestCase("PENDING", "KMC", 2012)]
        [TestCase("KMC-1", null, 2012)]
        [TestCase("KMC-1", "KMC", null)]
        public async Task Self_AnyMissingItem_BlocksGoingOnline(string licence, string? council, int? year)
        {
            var (user, doctor, _) = SeedDoctor(licence, council, year);
            var response = await new UpdateOwnOnlineStatusHandler(_context).Handle(new UpdateOwnOnlineStatusRequestModel { CallerUserId = user.UserID, IsOnlineNow = true }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(IsOnline(doctor), Is.False);
        }

        [Test]
        public async Task Staff_CannotSwitchAnUnconfirmedDoctorOnline_EitherAndConfirmedWorks()
        {
            var (_, unconfirmed, hospital) = SeedDoctor(licence: "PENDING", council: null, year: null);
            var handler = new UpdateDoctorOnlineStatusHandler(_context);

            var refused = await handler.Handle(new UpdateDoctorOnlineStatusRequestModel { HospitalId = hospital.HospitalID, DoctorId = unconfirmed.DoctorID, IsOnlineNow = true }, CancellationToken.None);
            Assert.That(refused.Success, Is.False);
            Assert.That(IsOnline(unconfirmed), Is.False);

            unconfirmed.LicenseNumber = "KMC-9"; unconfirmed.MedicalCouncil = "KMC"; unconfirmed.RegistrationYear = 2010;
            _context.SaveChanges();
            var allowed = await handler.Handle(new UpdateDoctorOnlineStatusRequestModel { HospitalId = hospital.HospitalID, DoctorId = unconfirmed.DoctorID, IsOnlineNow = true }, CancellationToken.None);
            Assert.That(allowed.Success, Is.True, allowed.Message);
            Assert.That(IsOnline(unconfirmed), Is.True);
        }
    }
}
