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
    public class SendPathologyLineToExternalLabHandlerTests
    {
        private AppDbContext _context = null!;
        private SendPathologyLineToExternalLabHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SendPathologyLineToExternalLabHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, PathologyOrderLine Line, Guid DefaultLabId) SeedSampleCollectedOutsourcedLine(decimal? costPrice = 80m)
        {
            var hospitalId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();
            var defaultLabId = Guid.NewGuid();

            _context.PathologyExternalLab.Add(new PathologyExternalLab { ExternalLabId = defaultLabId, HospitalId = hospitalId, LabName = "Metro Diagnostics", IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "GEN-001",
                TestName = "Genetic Panel",
                IsActive = true,
                IsOutsourced = true,
                DefaultExternalLabId = defaultLabId,
                CostPrice = costPrice,
            });
            _context.PathologyOrder.Add(new PathologyOrder { OrderId = orderId, HospitalId = hospitalId, PatientId = "PTID00000001", OrderNo = "LR-1", OrderDate = DateTime.UtcNow, Status = "IN_PROGRESS" });
            var line = new PathologyOrderLine { OrderLineId = orderLineId, HospitalId = hospitalId, OrderId = orderId, TestId = testId, Status = "SAMPLE_COLLECTED" };
            _context.PathologyOrderLine.Add(line);
            _context.SaveChanges();

            return (hospitalId, line, defaultLabId);
        }

        [Test]
        public async Task Handle_SampleCollectedOutsourcedLine_DefaultsToTestsDefaultLabAndSnapshotsCost()
        {
            var (hospitalId, line, defaultLabId) = SeedSampleCollectedOutsourcedLine(costPrice: 80m);

            var result = await _handler.Handle(new SendPathologyLineToExternalLabCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
                ExternalLabRefNo = "REF-123",
                LoggedInUserName = "tech1",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyOrderLine.Single(l => l.OrderLineId == line.OrderLineId);
            Assert.That(saved.Status, Is.EqualTo("SENT_TO_EXTERNAL_LAB"));
            Assert.That(saved.ExternalLabId, Is.EqualTo(defaultLabId));
            Assert.That(saved.ExternalLabRefNo, Is.EqualTo("REF-123"));
            Assert.That(saved.ExternalLabCost, Is.EqualTo(80m));
            Assert.That(saved.SentToExternalLabAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_ExplicitExternalLabId_OverridesTestsDefault()
        {
            var (hospitalId, line, _) = SeedSampleCollectedOutsourcedLine();
            var overrideLabId = Guid.NewGuid();
            _context.PathologyExternalLab.Add(new PathologyExternalLab { ExternalLabId = overrideLabId, HospitalId = hospitalId, LabName = "Alt Lab", IsActive = true });
            _context.SaveChanges();

            await _handler.Handle(new SendPathologyLineToExternalLabCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
                ExternalLabId = overrideLabId,
            }, CancellationToken.None);

            var saved = _context.PathologyOrderLine.Single(l => l.OrderLineId == line.OrderLineId);
            Assert.That(saved.ExternalLabId, Is.EqualTo(overrideLabId));
        }

        [Test]
        public async Task Handle_LineNotSampleCollected_ReturnsFalse()
        {
            var (hospitalId, line, _) = SeedSampleCollectedOutsourcedLine();
            line.Status = "PENDING";
            _context.SaveChanges();

            var result = await _handler.Handle(new SendPathologyLineToExternalLabCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Handle_TestNotOutsourced_ReturnsFalse()
        {
            var (hospitalId, line, _) = SeedSampleCollectedOutsourcedLine();
            var test = _context.PathologyTestMaster.Single(t => t.TestId == line.TestId);
            test.IsOutsourced = false;
            _context.SaveChanges();

            var result = await _handler.Handle(new SendPathologyLineToExternalLabCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Handle_NoDefaultAndNoExplicitLab_ReturnsFalse()
        {
            var (hospitalId, line, _) = SeedSampleCollectedOutsourcedLine();
            var test = _context.PathologyTestMaster.Single(t => t.TestId == line.TestId);
            test.DefaultExternalLabId = null;
            _context.SaveChanges();

            var result = await _handler.Handle(new SendPathologyLineToExternalLabCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }
    }
}
