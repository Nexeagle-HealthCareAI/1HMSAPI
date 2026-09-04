using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PharmacyRetailCheckoutCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private PharmacyRetailCheckoutCommandHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new PharmacyRetailCheckoutCommandHandler(_context, _mediatorMock.Object, NullLogger<PharmacyRetailCheckoutCommandHandler>.Instance);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private InventoryItem SeedDrugItem(Guid chargeId)
        {
            var item = new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                ItemCode = "PARA-500",
                ItemName = "Paracetamol 500mg",
                Category = "DRUG",
                Unit = "TAB",
                IsTaxable = true,
                GstSlabPercent = 12,
                CurrentStock = 100,
                MinStockLevel = 0,
                ReorderQty = 0,
                ChargeId = chargeId,
                DefaultRate = 2,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.InventoryItem.Add(item);
            return item;
        }

        private PharmacyRetailCheckoutCommand ValidRequest(Guid inventoryItemId) => new()
        {
            HospitalId = _hospitalId,
            StoreId = _storeId,
            // Every checkout requires a real, searched-or-registered patient now — see
            // Handle_DirectCash_NoPatientId_ReturnsError for the guard itself.
            PatientId = "PT-100",
            Items = new List<PharmacyCartItem>
            {
                new() { InventoryItemId = inventoryItemId, Qty = 10, Rate = 2, DiscountPercent = 0 }
            },
            TotalAmount = 20,
            PaidAmount = 20,
            PaymentMode = "CASH",
        };

        private void MockSuccessfulMovementAndCharge(Guid chargeId, Guid chargeEventId, List<AllocatedBatchDetail>? allocations = null)
        {
            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel
                {
                    Success = true,
                    InventoryMovementId = Guid.NewGuid(),
                    AllocatedBatchDetails = allocations ?? new List<AllocatedBatchDetail>()
                });

            // DisplayName isn't nullable on the real BillingChargeEvent table — matching only on
            // ChargeId let a prior bug (DisplayName never set) through undetected, since a real DB
            // insert would reject it but the mock happily returned success either way.
            _mediatorMock.Setup(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.Charges!.Count == 1
                    && r.Charges[0].ChargeId == chargeId
                    && !string.IsNullOrWhiteSpace(r.Charges[0].DisplayName)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel
                {
                    Success = true,
                    Data = new AddChargesData { ChargeEvents = new() { new ChargeEventDetail { ChargeEventId = chargeEventId } } },
                });
        }

        private void SeedChargeEvent(Guid chargeEventId, Guid encounterId)
        {
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                NetAmount = 20,
                TaxAmount = 2,
                GrossAmount = 20,
                DiscountAmount = 0,
                CreatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_DirectCash_HappyPath_CreatesInvoiceAndReturnsAllocatedBatches()
        {
            var chargeId = Guid.NewGuid();
            var chargeEventId = Guid.NewGuid();
            var item = SeedDrugItem(chargeId);
            await _context.SaveChangesAsync();

            var batchId = Guid.NewGuid();
            var allocations = new List<AllocatedBatchDetail>
            {
                new() { BatchId = batchId, BatchNumber = "B-001", ExpiryDate = DateTime.UtcNow.AddMonths(6), Mrp = 3.5m, AllocatedQty = 10 }
            };
            MockSuccessfulMovementAndCharge(chargeId, chargeEventId, allocations);

            var request = ValidRequest(item.InventoryItemId);

            // Encounter/ChargeEvent get created inside the handler before we can seed the charge
            // event row keyed by the real EncounterId, so intercept the AddChargeEvent call to
            // seed it once the handler's EncounterId is known.
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<AddChargeEventRequestModel, CancellationToken>((req, _) =>
                {
                    SeedChargeEvent(chargeEventId, req.EncounterId);
                    _context.SaveChanges();
                    return Task.FromResult(new AddChargeEventResponseModel
                    {
                        Success = true,
                        Data = new AddChargesData { ChargeEvents = new() { new ChargeEventDetail { ChargeEventId = chargeEventId } } },
                    });
                });

            _mediatorMock.Setup(m => m.Send(It.IsAny<AddPaymentEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddPaymentEventResponseModel { Success = true });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.InvoiceId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(response.InvoiceNo, Is.Not.Null.And.Not.Empty);
            Assert.That(response.AllocatedBatches, Has.Count.EqualTo(1));
            Assert.That(response.AllocatedBatches[0].BatchNumber, Is.EqualTo("B-001"));
            Assert.That(response.AllocatedBatches[0].Mrp, Is.EqualTo(3.5m));
        }

        [Test]
        public async Task Handle_NoBillableItems_ReturnsError()
        {
            // Item exists but has no ChargeId configured, so no ChargeDetail is ever built.
            var item = SeedDrugItem(Guid.Empty);
            item.ChargeId = null;
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid() });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No billable items"));
        }

        [Test]
        public async Task Handle_StockIssueFails_ReturnsErrorAndNeverPostsCharge()
        {
            var item = SeedDrugItem(Guid.NewGuid());
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock." });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Insufficient stock"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_DirectCash_NoPatientId_ReturnsError()
        {
            // A plain cash sale used to be allowed to go out with no patient at all -- the
            // unconditional guard closes that gap so a regulated (H/H1/X) dispense can never be
            // untraceable, not just the admission-billed path.
            var item = SeedDrugItem(Guid.NewGuid());
            await _context.SaveChangesAsync();

            var request = ValidRequest(item.InventoryItemId);
            request.PatientId = null;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("patient is required"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_PostToAdmissionDayBill_NoPatientId_ReturnsError()
        {
            var item = SeedDrugItem(Guid.NewGuid());
            await _context.SaveChangesAsync();

            var request = ValidRequest(item.InventoryItemId);
            request.SettlementMode = PharmacySettlementMode.PostToAdmissionDayBill;
            request.PatientId = null;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("patient is required"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_PostToAdmissionDayBill_NoActiveAdmission_ReturnsError()
        {
            var item = SeedDrugItem(Guid.NewGuid());
            await _context.SaveChangesAsync();

            var request = ValidRequest(item.InventoryItemId);
            request.SettlementMode = PharmacySettlementMode.PostToAdmissionDayBill;
            request.PatientId = "PT-999"; // no Admission row seeded for this patient

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No active admission"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_PostToAdmissionDayBill_ActiveAdmission_PostsChargeAgainstAdmissionEncounter_NoInvoiceCreated()
        {
            var chargeId = Guid.NewGuid();
            var chargeEventId = Guid.NewGuid();
            var item = SeedDrugItem(chargeId);

            var admissionEncounterId = Guid.NewGuid();
            _context.Encounter.Add(new Encounter
            {
                EncounterId = admissionEncounterId,
                HospitalId = _hospitalId,
                PatientId = "PT-100",
                EncounterTypeCode = "IPD",
                SourceType = "ADMISSION",
                StatusCode = BillingConstants.EncounterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.Admission.Add(new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT-100",
                AdmissionNo = "ADM-0001",
                EncounterId = admissionEncounterId,
                StatusCode = IpdConstants.AdmissionStatus.Admitted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid() });

            _mediatorMock.Setup(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.EncounterId == admissionEncounterId), It.IsAny<CancellationToken>()))
                .Returns<AddChargeEventRequestModel, CancellationToken>((req, _) =>
                {
                    SeedChargeEvent(chargeEventId, req.EncounterId);
                    _context.SaveChanges();
                    return Task.FromResult(new AddChargeEventResponseModel
                    {
                        Success = true,
                        Data = new AddChargesData { ChargeEvents = new() { new ChargeEventDetail { ChargeEventId = chargeEventId } } },
                    });
                });

            var request = ValidRequest(item.InventoryItemId);
            request.SettlementMode = PharmacySettlementMode.PostToAdmissionDayBill;
            request.PatientId = "PT-100";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.EncounterId, Is.EqualTo(admissionEncounterId));
            Assert.That(response.InvoiceId, Is.EqualTo(Guid.Empty));
            Assert.That(response.InvoiceNo, Is.Null);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddPaymentEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);

            var encounterAfter = await _context.Encounter.FindAsync(admissionEncounterId);
            Assert.That(encounterAfter!.StatusCode, Is.EqualTo(BillingConstants.EncounterStatus.Open), "Admission's Encounter must stay open — IPD workflow manages its lifecycle, not the pharmacy checkout.");
        }
    }
}
