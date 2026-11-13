using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>A patient edits their own details on Doctor Dekho; the hospital's registration is never touched.</summary>
    [TestFixture]
    public class PatientAccountProfileTests
    {
        private AppDbContext _context = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private const string Mobile = "9876543210";

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.GetPublicPatientProfileResponseModel> Save(Action<UpdatePublicPatientProfileRequestModel>? change = null, string mobile = Mobile)
        {
            var r = new UpdatePublicPatientProfileRequestModel { Mobile = mobile, FullName = "Aquib Khan", Age = 32, AgeUnit = "y", Sex = "male", Email = "aquib@example.com", GuardianName = "Abx", GuardianRelation = "C/O" };
            change?.Invoke(r);
            return new UpdatePublicPatientProfileHandler(_context).Handle(r, Ct);
        }

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.GetPublicPatientProfileResponseModel> Read() =>
            new GetPublicPatientProfileHandler(_context).Handle(new GetPublicPatientProfileRequestModel { Mobile = Mobile }, Ct);

        [Test]
        public async Task SavingCreatesTheAccountProfile_AndTheNextReadReturnsIt()
        {
            var saved = await Save();
            var read = await Read();

            Assert.That(saved.Success, Is.True, saved.Message);
            Assert.That((read.FullName, read.Age, read.AgeUnit, read.Sex, read.Email, read.GuardianName, read.GuardianRelation),
                Is.EqualTo(("Aquib Khan", (int?)32, "Y", "Male", "aquib@example.com", "Abx", "C/O")));
            Assert.That(read.Source, Is.EqualTo("ACCOUNT"));
        }

        [Test]
        public async Task SavingAgainChangesTheSameRow()
        {
            await Save();
            await Save(r => { r.FullName = "Aquib K. Khan"; r.Email = null; });

            Assert.That(_context.PatientAccountProfiles.Count(), Is.EqualTo(1));
            var read = await Read();
            Assert.That(read.FullName, Is.EqualTo("Aquib K. Khan"));
            Assert.That(read.Email, Is.Null);
        }

        [Test]
        public async Task BeforeAnySave_TheLatestHospitalRegistrationIsShown_AndSavingNeverChangesIt()
        {
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), PatientId = "P1", Mobile = Mobile, FullName = "Aquiib", Age = 32, AgeUnit = "Y", Sex = "Male", RegisteredAt = DateTime.UtcNow, HospitalId = Guid.NewGuid() });
            await _context.SaveChangesAsync(Ct);

            var before = await Read();
            await Save(r => r.FullName = "Aquib Khan");
            var after = await Read();

            Assert.That((before.FullName, before.Source), Is.EqualTo(("Aquiib", "BOOKING")));
            Assert.That((after.FullName, after.Source), Is.EqualTo(("Aquib Khan", "ACCOUNT")));
            Assert.That(_context.PatientRegistrations.Single().FullName, Is.EqualTo("Aquiib"), "the hospital's record is not changed");
        }

        [Test]
        public async Task EachNumberHasItsOwnProfile()
        {
            await Save();
            await Save(r => r.FullName = "Someone Else", "9000000001");

            Assert.That(_context.PatientAccountProfiles.Count(), Is.EqualTo(2));
            Assert.That((await Read()).FullName, Is.EqualTo("Aquib Khan"));
        }

        [TestCase(null, "Enter your first name.")]
        [TestCase("   ", "Enter your first name.")]
        [TestCase("A", "too short")]
        public async Task ANameIsRequired(string? name, string expected)
        {
            var res = await Save(r => r.FullName = name);

            Assert.That(res.Success, Is.False);
            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(res.Message, Does.Contain(expected));
            Assert.That(_context.PatientAccountProfiles.Any(), Is.False);
        }

        [Test]
        public async Task BadValuesAreRefused_WithAMessage()
        {
            Assert.That((await Save(r => r.Age = 130)).Message, Does.Contain("Age"));
            Assert.That((await Save(r => r.Age = -1)).Message, Does.Contain("Age"));
            Assert.That((await Save(r => r.AgeUnit = "X")).Message, Does.Contain("unit"));
            Assert.That((await Save(r => r.Sex = "robot")).Message, Does.Contain("Male, Female or Other"));
            Assert.That((await Save(r => r.Email = "not-an-email")).Message, Does.Contain("email"));
            Assert.That((await Save(r => r.GuardianName = new string('x', 201))).Message, Does.Contain("too long"));
            Assert.That(_context.PatientAccountProfiles.Any(), Is.False);
        }

        [Test]
        public async Task OptionalThingsMayBeLeftOut_AndSpacesAreTidied()
        {
            var res = await Save(r => { r.Age = null; r.AgeUnit = null; r.Sex = null; r.Email = "  "; r.GuardianName = null; r.GuardianRelation = null; r.FullName = "  Aquib   Khan "; });

            Assert.That(res.Success, Is.True, res.Message);
            var row = _context.PatientAccountProfiles.Single();
            Assert.That(row.FullName, Is.EqualTo("Aquib Khan"));
            Assert.That(row.Age, Is.Null);
            Assert.That(row.AgeUnit, Is.Null);
            Assert.That(row.Sex, Is.Null);
            Assert.That(row.Email, Is.Null);
        }

        [Test]
        public async Task WithoutAMobile_NothingIsSaved()
        {
            var res = await Save(mobile: "");
            Assert.That(res.StatusCode, Is.EqualTo(401));
            Assert.That(_context.PatientAccountProfiles.Any(), Is.False);
        }

        [TestCase("m", "Male")]
        [TestCase("FEMALE", "Female")]
        [TestCase("other", "Other")]
        public void SexIsStoredInTheSameWordsWhateverTheCase(string input, string expected) => Assert.That(PatientProfileRules.NormalizeSex(input), Is.EqualTo(expected));
    }
}
