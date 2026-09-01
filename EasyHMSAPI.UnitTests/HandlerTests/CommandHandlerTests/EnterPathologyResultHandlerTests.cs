using System;
using System.Linq;
using System.Text.Json;
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
    // Covers Phase 3 of the 1Lab plan: EnterPathologyResultHandler now recomputes
    // PathologyResultFlagCalculator's flag server-side and persists it alongside the raw value,
    // instead of storing the raw value alone. See PathologyResultFlagCalculatorTests.cs for the
    // calculator's own boundary-case coverage; these tests cover the handler's wiring around it
    // (schema lookup, patient age/gender resolution, HasCriticalFlag) rather than re-deriving flag
    // boundaries.
    [TestFixture]
    public class EnterPathologyResultHandlerTests
    {
        private AppDbContext _context = null!;
        private EnterPathologyResultHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new EnterPathologyResultHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private const string HemoglobinSchema =
            "{\"params\":[{\"name\":\"Hemoglobin\",\"unit\":\"g/dL\",\"maleMin\":13.5,\"maleMax\":17.5," +
            "\"femaleMin\":12.0,\"femaleMax\":15.5,\"childMin\":11.0,\"childMax\":14.5," +
            "\"criticalLow\":6.0,\"criticalHigh\":20.0}]}";

        private (Guid HospitalId, Guid OrderId, Guid OrderLineId) SeedOrder(
            string schemaJson, DateTime? dob, string? sex)
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();
            var patientId = "PTID" + Guid.NewGuid().ToString("N")[..8];

            _context.PatientRegistrations.Add(new PatientRegistration
            {
                PatientId = patientId,
                HospitalId = hospitalId,
                FullName = "Test Patient",
                DateOfBirth = dob,
                Sex = sex,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "CBC",
                ParameterSchemaJson = schemaJson,
                IsActive = true,
            });
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = patientId,
                OrderNo = "LAB-1",
                Status = "PLACED",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = orderLineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testId,
                Status = "PENDING",
            });
            _context.SaveChanges();

            return (hospitalId, orderId, orderLineId);
        }

        private JsonElement GetSavedEntry(Guid orderLineId, string paramName)
        {
            var result = _context.PathologyResult.Single(r => r.OrderLineId == orderLineId);
            var doc = JsonDocument.Parse(result.ResultValuesJson);
            return doc.RootElement.GetProperty(paramName);
        }

        [Test]
        public async Task Handle_CriticalLowValueForAdultMale_PersistsCriticalFlagAndSetsHasCriticalFlag()
        {
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-30), "MALE");

            var success = await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Hemoglobin\":\"5.0\"}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(success, Is.True);
            var entry = GetSavedEntry(orderLineId, "Hemoglobin");
            Assert.That(entry.GetProperty("value").GetString(), Is.EqualTo("5.0"));
            Assert.That(entry.GetProperty("flag").GetString(), Is.EqualTo("CRITICAL_LOW"));
            Assert.That(_context.PathologyResult.Single(r => r.OrderLineId == orderLineId).HasCriticalFlag, Is.True);
        }

        [Test]
        public async Task Handle_NormalValueForAdultMale_DoesNotSetHasCriticalFlag()
        {
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-30), "MALE");

            await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Hemoglobin\":\"14.5\"}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            var entry = GetSavedEntry(orderLineId, "Hemoglobin");
            Assert.That(entry.GetProperty("flag").GetString(), Is.EqualTo("NORMAL"));
            Assert.That(_context.PathologyResult.Single(r => r.OrderLineId == orderLineId).HasCriticalFlag, Is.False);
        }

        [Test]
        public async Task Handle_ChildPatient_UsesChildRangeNotAdultRange()
        {
            // 13.0 is within the child band (11.0-14.5) but above the adult male band's own
            // ceiling only if maleMax were lower -- here it's inside both, so instead check a
            // value that is LOW for an adult male (13.5 floor) but NORMAL for a 6-year-old
            // (11.0-14.5 floor) to prove the child band, not the adult one, was actually used.
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-6), "MALE");

            await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Hemoglobin\":\"12.0\"}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            var entry = GetSavedEntry(orderLineId, "Hemoglobin");
            Assert.That(entry.GetProperty("flag").GetString(), Is.EqualTo("NORMAL"));
        }

        [Test]
        public async Task Handle_NonNumericText_StaysNormalAndDoesNotThrow()
        {
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-30), "MALE");

            var success = await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Hemoglobin\":\"Hemolyzed sample\"}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(success, Is.True);
            var entry = GetSavedEntry(orderLineId, "Hemoglobin");
            Assert.That(entry.GetProperty("flag").GetString(), Is.EqualTo("NORMAL"));
        }

        // Custom/ad-hoc fields (added via OrderResultEntry.tsx's "+ Add Field") submit as
        // {value, unit} rather than a bare string -- both shapes must coexist in the one
        // submission without one breaking flag computation for the other.
        [Test]
        public async Task Handle_MixedSchemaAndCustomFieldEntry_ComputesFlagAndPersistsCustomUnit()
        {
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-30), "MALE");

            var success = await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Hemoglobin\":\"5.0\",\"Peripheral Smear\":{\"value\":\"Microcytic\",\"unit\":\"\"}}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(success, Is.True);

            var hemoglobin = GetSavedEntry(orderLineId, "Hemoglobin");
            Assert.That(hemoglobin.GetProperty("value").GetString(), Is.EqualTo("5.0"));
            Assert.That(hemoglobin.GetProperty("flag").GetString(), Is.EqualTo("CRITICAL_LOW"));
            Assert.That(_context.PathologyResult.Single(r => r.OrderLineId == orderLineId).HasCriticalFlag, Is.True);

            var custom = GetSavedEntry(orderLineId, "Peripheral Smear");
            Assert.That(custom.GetProperty("value").GetString(), Is.EqualTo("Microcytic"));
            Assert.That(custom.GetProperty("flag").GetString(), Is.EqualTo("NORMAL"));
        }

        [Test]
        public async Task Handle_CustomFieldWithUnit_RoundTripsUnitInSavedJson()
        {
            var (hospitalId, orderId, orderLineId) = SeedOrder(HemoglobinSchema, DateTime.UtcNow.AddYears(-30), "MALE");

            await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = orderLineId,
                ResultValuesJson = "{\"Vitamin D\":{\"value\":\"22\",\"unit\":\"ng/mL\"}}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            var entry = GetSavedEntry(orderLineId, "Vitamin D");
            Assert.That(entry.GetProperty("value").GetString(), Is.EqualTo("22"));
            Assert.That(entry.GetProperty("unit").GetString(), Is.EqualTo("ng/mL"));
        }

        [Test]
        public async Task Handle_UnknownOrderLine_ReturnsFalse()
        {
            var success = await _handler.Handle(new EnterPathologyResultCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OrderLineId = Guid.NewGuid(),
                ResultValuesJson = "{}",
                LoggedInUserId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(success, Is.False);
        }
    }
}
