using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    // Covers the cross-tenant leak fix: a line's TestId is trusted at write time now
    // (CreatePathologyOrderHandler validates it belongs to the order's hospital), but this handler
    // used to resolve PathologyTestMaster/PathologyResult/PathologyReport by their own PK alone, with
    // no HospitalId filter -- meaning a line pointing at another hospital's TestId (however it got
    // there) would still have that other hospital's test metadata read back through this order.
    [TestFixture]
    public class GetPathologyOrderByIdHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPathologyOrderByIdHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPathologyOrderByIdHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_LineTestBelongsToAnotherHospital_DoesNotResolveForeignTestMetadata()
        {
            var hospitalId = Guid.NewGuid();
            var otherHospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var foreignTestId = Guid.NewGuid();

            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = "PLACED",
            });
            // Belongs to a DIFFERENT hospital -- simulates a line that somehow ended up pointing at a
            // foreign TestId (the write-side fix now prevents this going forward).
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = foreignTestId,
                HospitalId = otherHospitalId,
                TestCode = "SECRET-CODE",
                TestName = "Other Hospital's Private Test",
                IsActive = true,
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = foreignTestId,
                Status = "PENDING",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrderByIdQuery { HospitalId = hospitalId, OrderId = orderId }, CancellationToken.None);

            Assert.That(result.Lines, Has.Count.EqualTo(1));
            Assert.That(result.Lines[0].TestName, Is.EqualTo("Unknown Test"));
            Assert.That(result.Lines[0].TestName, Does.Not.Contain("Private"));
            Assert.That(result.Lines[0].TestCode, Is.EqualTo("Unknown Code"));
        }

        [Test]
        public async Task Handle_LineTestBelongsToSameHospital_ResolvesTestMetadata()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var testId = Guid.NewGuid();

            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-2",
                Status = "PLACED",
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "Complete Blood Count (CBC)",
                IsActive = true,
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testId,
                Status = "PENDING",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyOrderByIdQuery { HospitalId = hospitalId, OrderId = orderId }, CancellationToken.None);

            Assert.That(result.Lines, Has.Count.EqualTo(1));
            Assert.That(result.Lines[0].TestName, Is.EqualTo("Complete Blood Count (CBC)"));
            Assert.That(result.Lines[0].TestCode, Is.EqualTo("HEM-CBC"));
        }
    }
}
