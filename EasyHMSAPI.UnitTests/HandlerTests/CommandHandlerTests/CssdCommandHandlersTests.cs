using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Regression coverage for a real patient-safety gap found in a CSSD audit: ISSUE_TO_OT was only
    // blocked when a set was RETIRED, so a set still STERILIZING (biological indicator pending) or
    // QUARANTINED (indicator failed) could be issued to an OT case with no system pushback.
    [TestFixture]
    public class CssdCommandHandlersTests
    {
        private AppDbContext _context = null!;
        private CssdCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CssdCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, InstrumentSet Set) SeedSet(string status)
        {
            var hospitalId = Guid.NewGuid();
            var set = new InstrumentSet
            {
                InstrumentSetId = Guid.NewGuid(),
                HospitalId = hospitalId,
                SetCode = "SET-1",
                SetName = "General Surgery Set",
                CurrentStatus = status,
                IsActive = true,
            };
            _context.InstrumentSet.Add(set);
            _context.SaveChanges();
            return (hospitalId, set);
        }

        [TestCase(IpdConstants.InstrumentSetStatus.Sterilizing)]
        [TestCase(IpdConstants.InstrumentSetStatus.Quarantined)]
        [TestCase(IpdConstants.InstrumentSetStatus.Washing)]
        [TestCase(IpdConstants.InstrumentSetStatus.Packed)]
        [TestCase(IpdConstants.InstrumentSetStatus.ReturnedSoiled)]
        [TestCase(IpdConstants.InstrumentSetStatus.InUse)]
        [TestCase(IpdConstants.InstrumentSetStatus.Sterile)]
        public async Task IssueToOt_SetNotAvailable_Rejects(string status)
        {
            var (hospitalId, set) = SeedSet(status);

            var response = await _handler.Handle(new RecordInstrumentSetMovementRequestModel
            {
                HospitalId = hospitalId,
                InstrumentSetId = set.InstrumentSetId,
                MovementType = IpdConstants.InstrumentSetMovementType.IssueToOt,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var saved = _context.InstrumentSet.Single(s => s.InstrumentSetId == set.InstrumentSetId);
            Assert.That(saved.CurrentStatus, Is.EqualTo(status), "status must not change on a rejected movement");
        }

        [Test]
        public async Task IssueToOt_SetAvailable_Succeeds()
        {
            var (hospitalId, set) = SeedSet(IpdConstants.InstrumentSetStatus.Available);

            var response = await _handler.Handle(new RecordInstrumentSetMovementRequestModel
            {
                HospitalId = hospitalId,
                InstrumentSetId = set.InstrumentSetId,
                MovementType = IpdConstants.InstrumentSetMovementType.IssueToOt,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.InstrumentSet.Single(s => s.InstrumentSetId == set.InstrumentSetId);
            Assert.That(saved.CurrentStatus, Is.EqualTo(IpdConstants.InstrumentSetStatus.InUse));
        }

        [Test]
        public async Task OtherMovementTypes_StillAllowedRegardlessOfStatus()
        {
            // Only ISSUE_TO_OT gets the new gate -- e.g. SEND_TO_WASH from RETURNED_SOILED must
            // keep working exactly as before.
            var (hospitalId, set) = SeedSet(IpdConstants.InstrumentSetStatus.ReturnedSoiled);

            var response = await _handler.Handle(new RecordInstrumentSetMovementRequestModel
            {
                HospitalId = hospitalId,
                InstrumentSetId = set.InstrumentSetId,
                MovementType = IpdConstants.InstrumentSetMovementType.SendToWash,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
        }
    }
}
