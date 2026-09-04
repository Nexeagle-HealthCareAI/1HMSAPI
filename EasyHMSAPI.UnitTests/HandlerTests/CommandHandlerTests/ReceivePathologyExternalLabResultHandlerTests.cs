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
    public class ReceivePathologyExternalLabResultHandlerTests
    {
        private AppDbContext _context = null!;
        private ReceivePathologyExternalLabResultHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ReceivePathologyExternalLabResultHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, PathologyOrderLine Line) SeedSentLine(string status = "SENT_TO_EXTERNAL_LAB")
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder { OrderId = orderId, HospitalId = hospitalId, PatientId = "PTID00000001", OrderNo = "LR-1", OrderDate = DateTime.UtcNow, Status = "IN_PROGRESS" });
            var line = new PathologyOrderLine { OrderLineId = orderLineId, HospitalId = hospitalId, OrderId = orderId, TestId = Guid.NewGuid(), Status = status };
            _context.PathologyOrderLine.Add(line);
            _context.SaveChanges();
            return (hospitalId, line);
        }

        [Test]
        public async Task Handle_SentLine_TransitionsToResultReceivedAndSetsTimestamp()
        {
            var (hospitalId, line) = SeedSentLine();

            var result = await _handler.Handle(new ReceivePathologyExternalLabResultCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
                LoggedInUserName = "tech1",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyOrderLine.Single(l => l.OrderLineId == line.OrderLineId);
            Assert.That(saved.Status, Is.EqualTo("RESULT_RECEIVED_FROM_EXTERNAL_LAB"));
            Assert.That(saved.ExternalLabReceivedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_LineNotSentToExternalLab_ReturnsFalse()
        {
            var (hospitalId, line) = SeedSentLine(status: "SAMPLE_COLLECTED");

            var result = await _handler.Handle(new ReceivePathologyExternalLabResultCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Handle_OrderLineNotFound_ReturnsFalse()
        {
            var result = await _handler.Handle(new ReceivePathologyExternalLabResultCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OrderLineId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }
    }
}
