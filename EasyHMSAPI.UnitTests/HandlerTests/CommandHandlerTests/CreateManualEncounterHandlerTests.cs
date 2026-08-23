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
    public class CreateManualEncounterHandlerTests
    {
        private AppDbContext _context = null!;
        private CreateManualEncounterHandler _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CreateManualEncounterHandler(_context);
            _hospitalId = Guid.NewGuid();

            _context.PatientRegistrations.Add(new PatientRegistration
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
            });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_AcceptsAPastServiceDate()
        {
            var pastDate = DateTime.UtcNow.AddDays(-4).Date;
            var response = await _handler.Handle(new CreateManualEncounterRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterType = "OPD",
                ServiceDate = pastDate,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var encounter = _context.Encounter.Single(e => e.EncounterId == response.Data!.EncounterId);
            Assert.That(encounter.ServiceDate, Is.EqualTo(pastDate));
        }

        [Test]
        public async Task Handle_DefaultsServiceDateToNull_WhenOmitted()
        {
            var response = await _handler.Handle(new CreateManualEncounterRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterType = "OPD",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var encounter = _context.Encounter.Single(e => e.EncounterId == response.Data!.EncounterId);
            Assert.That(encounter.ServiceDate, Is.Null);
        }

        [Test]
        public async Task Handle_RejectsAFutureServiceDate()
        {
            var response = await _handler.Handle(new CreateManualEncounterRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterType = "OPD",
                ServiceDate = DateTime.UtcNow.AddDays(1),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("future"));
            Assert.That(_context.Encounter.Count(), Is.EqualTo(0));
        }
    }
}
