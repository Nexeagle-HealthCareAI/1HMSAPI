using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Lab integrity: results were overwritten in place after the report went out, panic values were never
    /// escalated, reports had no sign-off, and discontinuing an IPD lab order left the lab line alive.
    /// </summary>
    [TestFixture]
    public class PathologyIntegrityTests
    {
        private const string HemoglobinSchema =
            "{\"params\":[{\"name\":\"Hemoglobin\",\"unit\":\"g/dL\",\"maleMin\":13.5,\"maleMax\":17.5,\"criticalLow\":6.0,\"criticalHigh\":20.0}]}";

        private AppDbContext _context = null!;
        private EnterPathologyResultHandler _enter = null!;
        private Guid _hospitalId, _orderId, _lineId, _testId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _enter = new EnterPathologyResultHandler(_context);
            _hospitalId = Guid.NewGuid();
            _orderId = Guid.NewGuid();
            _lineId = Guid.NewGuid();
            _testId = Guid.NewGuid();
            var patientId = "PT" + Guid.NewGuid().ToString("N")[..8];

            _context.PatientRegistrations.Add(new PatientRegistration { PatientId = patientId, HospitalId = _hospitalId, FullName = "Pat", DateOfBirth = DateTime.UtcNow.AddYears(-40), Sex = "Male" });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = _testId, HospitalId = _hospitalId, TestCode = "CBC", TestName = "CBC", ParameterSchemaJson = HemoglobinSchema, IsActive = true });
            _context.PathologyOrder.Add(new PathologyOrder { OrderId = _orderId, HospitalId = _hospitalId, PatientId = patientId, OrderNo = "LAB-1", Status = "PLACED", AdmissionId = Guid.NewGuid() });
            _context.PathologyOrderLine.Add(new PathologyOrderLine { OrderLineId = _lineId, HospitalId = _hospitalId, OrderId = _orderId, TestId = _testId, Status = "PENDING" });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private EnterPathologyResultCommand Command(string hb, string? reason = null, string? interpretation = null) => new()
        {
            HospitalId = _hospitalId, OrderId = _orderId, OrderLineId = _lineId,
            ResultValuesJson = "{\"Hemoglobin\":\"" + hb + "\"}", Interpretation = interpretation,
            AmendmentReason = reason, LoggedInUserName = "tech", LoggedInUserId = Guid.NewGuid(),
        };

        private async Task<Guid> GenerateReportFor(string status = "GENERATED")
        {
            var reportId = Guid.NewGuid();
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = _hospitalId, OrderId = _orderId, ReportNo = "R-1", Status = status, GeneratedAt = DateTime.UtcNow, ApprovedAt = status == "VERIFIED" ? DateTime.UtcNow : null });
            var line = await _context.PathologyOrderLine.FirstAsync(l => l.OrderLineId == _lineId);
            line.ReportId = reportId;
            (await _context.PathologyResult.FirstAsync(r => r.OrderLineId == _lineId)).ReportId = reportId;
            await _context.SaveChangesAsync();
            return reportId;
        }

        [Test]
        public async Task EditBeforeAnyReport_KeepsThePreviousVersionButNeedsNoReason()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            await _enter.Handle(Command("15.0"), CancellationToken.None);

            var history = await _context.PathologyResultHistory.SingleAsync();
            Assert.That(history.PreviousValuesJson, Does.Contain("14.0"));
            Assert.That(history.ChangeReason, Is.Null);
            Assert.That(history.ReportId, Is.Null);
        }

        [Test]
        public async Task ResavingIdenticalValues_WritesNoHistory()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            await _enter.Handle(Command("14.0"), CancellationToken.None);

            Assert.That(await _context.PathologyResultHistory.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task AmendingAReportedResult_WithoutAReason_IsRefusedAndNothingChanges()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            await GenerateReportFor();

            Assert.ThrowsAsync<AmendmentReasonRequiredException>(() => _enter.Handle(Command("9.0"), CancellationToken.None));
            Assert.ThrowsAsync<AmendmentReasonRequiredException>(() => _enter.Handle(Command("9.0", reason: "typo"), CancellationToken.None));

            Assert.That((await _context.PathologyResult.AsNoTracking().SingleAsync()).ResultValuesJson, Does.Contain("14.0"));
            Assert.That(await _context.PathologyResultHistory.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task AmendingAReportedResult_WithAReason_KeepsHistoryAndMarksTheReportAmended()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            var reportId = await GenerateReportFor("VERIFIED");

            await _enter.Handle(Command("15.5", reason: "Transcription error - re-read from analyser"), CancellationToken.None);

            var history = await _context.PathologyResultHistory.SingleAsync();
            Assert.That(history.PreviousValuesJson, Does.Contain("14.0"));
            Assert.That(history.ChangeReason, Does.Contain("Transcription error"));
            Assert.That(history.ReportId, Is.EqualTo(reportId));
            Assert.That(history.ChangedBy, Is.EqualTo("tech"));

            var report = await _context.PathologyReport.AsNoTracking().SingleAsync();
            Assert.That(report.Status, Is.EqualTo("AMENDED"));
            Assert.That(report.ApprovedAt, Is.Null, "a verification covered the old values");
        }

        [Test]
        public async Task CriticalValue_RaisesOneCriticalAlert_NotADuplicateWhileOpen_AndNoneForNormalValues()
        {
            await _enter.Handle(Command("14.5"), CancellationToken.None);
            Assert.That(await _context.Alert.CountAsync(), Is.EqualTo(0));

            await _enter.Handle(Command("5.0"), CancellationToken.None);
            var alert = await _context.Alert.SingleAsync();
            Assert.That(alert.Severity, Is.EqualTo("CRITICAL"));
            Assert.That(alert.AlertCode, Is.EqualTo("CRITICAL_LAB_RESULT"));
            Assert.That(alert.Body, Does.Contain("Hemoglobin 5.0"));
            Assert.That(alert.SourceRefId, Is.EqualTo(_lineId.ToString()));
            Assert.That(alert.AdmissionId, Is.Not.Null);
            Assert.That(alert.Status, Is.EqualTo("ACTIVE"));

            await _enter.Handle(Command("4.5"), CancellationToken.None);
            Assert.That(await _context.Alert.CountAsync(), Is.EqualTo(1), "one open alert per line");

            alert.Status = "ACKNOWLEDGED";
            await _context.SaveChangesAsync();
            await _enter.Handle(Command("4.0"), CancellationToken.None);
            Assert.That(await _context.Alert.CountAsync(), Is.EqualTo(2), "a new critical value after acknowledgement raises a new alert");
        }

        [Test]
        public async Task VerifyReport_RequiresPathologistDetails_AndIsRequiredAgainAfterAmendment()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            var reportId = await GenerateReportFor();
            var verify = new VerifyPathologyReportHandler(_context);
            VerifyPathologyReportCommand Cmd(string? name, string? reg) => new() { HospitalId = _hospitalId, OrderId = _orderId, ReportId = reportId, PathologistName = name, PathologistRegNo = reg, LoggedInUserId = Guid.NewGuid(), LoggedInUserName = "dr" };

            Assert.That((await verify.Handle(Cmd(null, "MCI-1"), CancellationToken.None)).Success, Is.False);
            Assert.That((await verify.Handle(Cmd("Dr Rao", ""), CancellationToken.None)).Success, Is.False);
            var ok = await verify.Handle(Cmd("Dr Rao", "MCI-1"), CancellationToken.None);
            Assert.That(ok.Success, Is.True, ok.Message);

            var report = await _context.PathologyReport.AsNoTracking().SingleAsync();
            Assert.That(report.Status, Is.EqualTo("VERIFIED"));
            Assert.That(report.PathologistName, Is.EqualTo("Dr Rao"));
            Assert.That(report.ApprovedAt, Is.Not.Null);
            Assert.That((await verify.Handle(Cmd("Dr Rao", "MCI-1"), CancellationToken.None)).Message, Does.Contain("already verified"));

            await _enter.Handle(Command("13.9", reason: "Re-run after QC fail"), CancellationToken.None);
            Assert.That((await _context.PathologyReport.AsNoTracking().SingleAsync()).Status, Is.EqualTo("AMENDED"));
            Assert.That((await verify.Handle(Cmd("Dr Rao", "MCI-1"), CancellationToken.None)).Success, Is.True, "an amended report can be verified again");
        }

        [Test]
        public async Task VerifyReport_OfAnotherHospital_IsNotFound()
        {
            await _enter.Handle(Command("14.0"), CancellationToken.None);
            var reportId = await GenerateReportFor();

            var result = await new VerifyPathologyReportHandler(_context).Handle(new VerifyPathologyReportCommand
            { HospitalId = Guid.NewGuid(), OrderId = _orderId, ReportId = reportId, PathologistName = "Dr X", PathologistRegNo = "1" }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not found"));
        }

        // ---- IPD CPOE discontinue vs. the linked lab line ----

        private async Task<Guid> SeedLinkedCpoeLine(string labStatus)
        {
            (await _context.PathologyOrderLine.FirstAsync(l => l.OrderLineId == _lineId)).Status = labStatus;
            var orderId = Guid.NewGuid();
            var cpoeLineId = Guid.NewGuid();
            _context.ClinicalOrder.Add(new ClinicalOrder { OrderId = orderId, HospitalId = _hospitalId, AdmissionId = Guid.NewGuid(), PatientId = "P", OrderType = IpdConstants.ClinicalOrderType.Lab, StatusCode = IpdConstants.ClinicalOrderStatus.Active, OrderedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.ClinicalOrderLine.Add(new ClinicalOrderLine
            {
                OrderLineId = cpoeLineId, OrderId = orderId, HospitalId = _hospitalId, ItemName = "CBC", Qty = 1,
                StatusCode = IpdConstants.ClinicalOrderLineStatus.Active, LinkedPathologyOrderLineId = _lineId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            return cpoeLineId;
        }

        [Test]
        public async Task DiscontinueLabOrderLine_BeforeCollection_CancelsTheLabLineAndItsOrder()
        {
            var cpoeLine = await SeedLinkedCpoeLine("PENDING");
            var handler = new ClinicalOrderCommandHandlers(_context, Mock.Of<IMediator>());

            var response = await handler.Handle(new DiscontinueClinicalOrderLineRequestModel { HospitalId = _hospitalId, OrderLineId = cpoeLine, LoggedInUserName = "dr" }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.LabLineCancelled, Is.True);
            Assert.That((await _context.PathologyOrderLine.AsNoTracking().SingleAsync()).Status, Is.EqualTo("CANCELLED"));
            Assert.That((await _context.PathologyOrder.AsNoTracking().SingleAsync()).Status, Is.EqualTo("CANCELLED"));
        }

        [Test]
        public async Task DiscontinueLabOrderLine_AfterCollectionOrResult_IsRefused_AndNothingChanges()
        {
            foreach (var status in new[] { "SAMPLE_COLLECTED", "RESULT_ENTERED" })
            {
                var cpoeLine = await SeedLinkedCpoeLine(status);
                var handler = new ClinicalOrderCommandHandlers(_context, Mock.Of<IMediator>());

                var response = await handler.Handle(new DiscontinueClinicalOrderLineRequestModel { HospitalId = _hospitalId, OrderLineId = cpoeLine, LoggedInUserName = "dr" }, CancellationToken.None);

                Assert.That(response.Success, Is.False, status);
                Assert.That(response.Message, Does.Contain("already collected or resulted"));
                Assert.That((await _context.ClinicalOrderLine.AsNoTracking().FirstAsync(l => l.OrderLineId == cpoeLine)).StatusCode, Is.EqualTo(IpdConstants.ClinicalOrderLineStatus.Active));
                Assert.That((await _context.PathologyOrderLine.AsNoTracking().SingleAsync()).Status, Is.EqualTo(status));
            }
        }
    }
}
