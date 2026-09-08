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
    // Report-level field values (Clinical History, Comments, ...) -- separate from per-line
    // results, saved once per order rather than per test line.
    [TestFixture]
    public class SaveOrderReportFieldsHandlerTests
    {
        private AppDbContext _context = null!;
        private SaveOrderReportFieldsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SaveOrderReportFieldsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Guid SeedOrder(Guid hospitalId)
        {
            var orderId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = "IN_PROGRESS",
            });
            _context.SaveChanges();
            return orderId;
        }

        [Test]
        public async Task Handle_OrderExists_PersistsReportFieldValues()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            const string valuesJson = "{\"cf_1\":\"Fever for 3 days\"}";

            var success = await _handler.Handle(new SaveOrderReportFieldsCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportFieldValuesJson = valuesJson,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(success, Is.True);
            var saved = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(saved.ReportFieldValuesJson, Is.EqualTo(valuesJson));
        }

        [Test]
        public async Task Handle_CalledAgain_OverwritesPreviousValues()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);

            await _handler.Handle(new SaveOrderReportFieldsCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportFieldValuesJson = "{\"cf_1\":\"first draft\"}",
            }, CancellationToken.None);

            await _handler.Handle(new SaveOrderReportFieldsCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportFieldValuesJson = "{\"cf_1\":\"corrected value\"}",
            }, CancellationToken.None);

            var saved = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(saved.ReportFieldValuesJson, Is.EqualTo("{\"cf_1\":\"corrected value\"}"));
        }

        [Test]
        public async Task Handle_OrderNotFound_ReturnsFalse()
        {
            var success = await _handler.Handle(new SaveOrderReportFieldsCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                ReportFieldValuesJson = "{}",
            }, CancellationToken.None);

            Assert.That(success, Is.False);
        }

        [Test]
        public async Task Handle_WrongHospitalId_ReturnsFalse()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);

            var success = await _handler.Handle(new SaveOrderReportFieldsCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = orderId,
                ReportFieldValuesJson = "{}",
            }, CancellationToken.None);

            Assert.That(success, Is.False);
        }
    }
}
