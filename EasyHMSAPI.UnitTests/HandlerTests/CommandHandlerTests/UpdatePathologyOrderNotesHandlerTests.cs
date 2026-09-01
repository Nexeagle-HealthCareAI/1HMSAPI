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
    public class UpdatePathologyOrderNotesHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdatePathologyOrderNotesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdatePathologyOrderNotesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_OrderNotFound_ReturnsFalse()
        {
            var result = await _handler.Handle(new UpdatePathologyOrderNotesCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                Notes = "test",
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Handle_OrderFound_UpdatesNotesAndStatFlag()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = "PLACED",
                Notes = "old notes",
                IsStat = false,
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new UpdatePathologyOrderNotesCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                Notes = "urgent recheck requested",
                IsStat = true,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(saved.Notes, Is.EqualTo("urgent recheck requested"));
            Assert.That(saved.IsStat, Is.True);
        }
    }
}
