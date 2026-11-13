using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Roster / patient-assignment / handover used to accept only MORNING, EVENING and NIGHT, so every
    /// hospital-defined shift (DAY, 12H-DAY …) was rejected with "Invalid shift code".
    /// </summary>
    [TestFixture]
    public class NursingShiftRulesTests
    {
        private AppDbContext _context = null!;
        private readonly Guid _hospital = Guid.NewGuid();

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void Configure(Guid hospitalId, params (string code, bool active)[] shifts)
        {
            foreach (var (code, active) in shifts)
                _context.NursingShift.Add(new NursingShift { HospitalId = hospitalId, ShiftCode = code, Label = code, IsActive = active });
            _context.SaveChanges();
        }

        [Test]
        public async Task NoConfiguredShifts_BuiltInsAccepted_CaseInsensitive_OthersRejected()
        {
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, " morning ", CancellationToken.None), Is.EqualTo("MORNING"));
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "NIGHT", CancellationToken.None), Is.EqualTo("NIGHT"));
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "DAY", CancellationToken.None), Is.Null);
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "", CancellationToken.None), Is.Null);
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, null, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task ConfiguredShifts_CustomCodeAccepted_BuiltInsNoLongerImplicit()
        {
            Configure(_hospital, ("DAY", true), ("12H-NIGHT", true));

            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "day", CancellationToken.None), Is.EqualTo("DAY"));
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "12h-night", CancellationToken.None), Is.EqualTo("12H-NIGHT"));
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "MORNING", CancellationToken.None), Is.Null,
                "a hospital that defined its own shifts only accepts those");
        }

        [Test]
        public async Task InactiveShift_Rejected_AndOtherHospitalsShiftsDoNotLeak()
        {
            Configure(_hospital, ("DAY", true), ("OLD", false));
            var other = Guid.NewGuid();
            Configure(other, ("SPLIT", true));

            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "OLD", CancellationToken.None), Is.Null);
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "SPLIT", CancellationToken.None), Is.Null);
            Assert.That(await NursingShiftRules.ResolveAsync(_context, other, "SPLIT", CancellationToken.None), Is.EqualTo("SPLIT"));
        }

        [Test]
        public async Task MalformedCode_Rejected()
        {
            Configure(_hospital, ("DAY", true));

            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, "DAY; DROP", CancellationToken.None), Is.Null);
            Assert.That(await NursingShiftRules.ResolveAsync(_context, _hospital, new string('A', 31), CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task RosterAssignment_AcceptsHospitalDefinedShift()
        {
            Configure(_hospital, ("DAY", true));
            var nurse = Guid.NewGuid();
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = _hospital, UserID = nurse });
            _context.BedMaster.Add(new BedMaster { BedId = Guid.NewGuid(), HospitalId = _hospital, WardCode = "W1", WardName = "Ward 1", BedCode = "B1", IsActive = true });
            _context.SaveChanges();
            var handler = new NurseShiftAssignmentCommandHandlers(_context);

            var custom = await handler.Handle(new AssignNurseShiftRequestModel { HospitalId = _hospital, NurseUserId = nurse, WardCode = "W1", ShiftCode = "day" }, CancellationToken.None);
            var builtIn = await handler.Handle(new AssignNurseShiftRequestModel { HospitalId = _hospital, NurseUserId = nurse, WardCode = "W1", ShiftCode = "MORNING" }, CancellationToken.None);

            Assert.That(custom.Success, Is.True, custom.Message);
            Assert.That(builtIn.Success, Is.False);
            Assert.That(builtIn.Message, Is.EqualTo("Invalid shift code."));
        }
    }
}
