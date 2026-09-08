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
    public class RegisterWalkInPatientHandlerTests
    {
        private AppDbContext _context = null!;
        private RegisterWalkInPatientHandler _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new RegisterWalkInPatientHandler(_context);
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewPatient_CreatesPatientRegistration()
        {
            var response = await _handler.Handle(new RegisterWalkInPatientRequestModel
            {
                HospitalId = _hospitalId,
                Patient = new Patient { FullName = "Raju Khan", Mobile = "9876543210", Age = 30, Sex = "Male" },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.PatientId, Is.Not.Null.And.Not.Empty);

            var saved = _context.PatientRegistrations.Single(p => p.PatientId == response.PatientId);
            Assert.That(saved.FullName, Is.EqualTo("Raju Khan"));
            Assert.That(saved.Mobile, Is.EqualTo("9876543210"));
            Assert.That(saved.Age, Is.EqualTo((short)30));
            Assert.That(saved.Sex, Is.EqualTo("Male"));
            Assert.That(saved.HospitalId, Is.EqualTo(_hospitalId));
        }

        [Test]
        public async Task Handle_ExistingMobileAndNameMatch_UpdatesInPlaceInsteadOfDuplicating()
        {
            var existing = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PTID00000001",
                FullName = "Raju Khan",
                Mobile = "9876543210",
                Age = 0,
                Sex = null,
            };
            _context.PatientRegistrations.Add(existing);
            _context.SaveChanges();

            var response = await _handler.Handle(new RegisterWalkInPatientRequestModel
            {
                HospitalId = _hospitalId,
                Patient = new Patient { FullName = "Raju Khan", Mobile = "9876543210", Age = 35, Sex = "Male" },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.PatientId, Is.EqualTo("PTID00000001"));
            Assert.That(_context.PatientRegistrations.Count(p => p.HospitalId == _hospitalId), Is.EqualTo(1));

            var saved = _context.PatientRegistrations.Single(p => p.PatientId == "PTID00000001");
            Assert.That(saved.Age, Is.EqualTo((short)35));
            Assert.That(saved.Sex, Is.EqualTo("Male"));
        }

        [Test]
        public async Task Handle_MissingMobile_ReturnsFailureWithoutThrowing()
        {
            var response = await _handler.Handle(new RegisterWalkInPatientRequestModel
            {
                HospitalId = _hospitalId,
                Patient = new Patient { FullName = "No Mobile" },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("mobile").IgnoreCase);
        }
    }
}
