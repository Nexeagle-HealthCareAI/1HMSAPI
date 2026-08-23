using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using EasyHMSAPI.UnitTests.TestUtils;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class GetDischargeSummaryQrCodeHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetDischargeSummaryQrCodeHandler _handler = null!;
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["WhatsAppBot:BaseUrl"]).Returns("https://whatsapp-dev-api.nexeagle.com");
            _handler = new GetDischargeSummaryQrCodeHandler(_context, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_SummaryNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetDischargeSummaryQrCodeRequestModel { HospitalId = Guid.NewGuid(), AdmissionId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_NoExistingAccessToken_MintsOneAndReturnsValidPng()
        {
            var hospitalId = Guid.NewGuid();
            var admissionId = Guid.NewGuid();
            var summaryId = Guid.NewGuid();
            _context.DischargeSummary.Add(new DischargeSummary { DischargeSummaryId = summaryId, HospitalId = hospitalId, AdmissionId = admissionId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDischargeSummaryQrCodeRequestModel { HospitalId = hospitalId, AdmissionId = admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Content, Is.Not.Null);
            Assert.That(response.Content![..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));

            var reloaded = await _context.DischargeSummary.FindAsync(summaryId);
            Assert.That(reloaded!.AccessToken, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Handle_ExistingAccessToken_ReusesIt_DoesNotMintANewOne()
        {
            var hospitalId = Guid.NewGuid();
            var admissionId = Guid.NewGuid();
            var summaryId = Guid.NewGuid();
            _context.DischargeSummary.Add(new DischargeSummary { DischargeSummaryId = summaryId, HospitalId = hospitalId, AdmissionId = admissionId, AccessToken = "FIXEDTOKEN123", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDischargeSummaryQrCodeRequestModel { HospitalId = hospitalId, AdmissionId = admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var reloaded = await _context.DischargeSummary.FindAsync(summaryId);
            Assert.That(reloaded!.AccessToken, Is.EqualTo("FIXEDTOKEN123"));
        }

        [Test]
        public async Task Handle_BaseUrlNotConfigured_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();
            var admissionId = Guid.NewGuid();
            _context.DischargeSummary.Add(new DischargeSummary { DischargeSummaryId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admissionId, AccessToken = "TOK", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var unconfigured = new GetDischargeSummaryQrCodeHandler(_context, new Mock<IConfiguration>().Object);
            var response = await unconfigured.Handle(new GetDischargeSummaryQrCodeRequestModel { HospitalId = hospitalId, AdmissionId = admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
