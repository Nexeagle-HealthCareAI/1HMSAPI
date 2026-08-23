using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetHospitalQrCodeHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetHospitalQrCodeHandler _handler = null!;

        // PNG signature: every valid PNG file starts with these 8 bytes.
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["WhatsAppBot:BaseUrl"]).Returns("https://whatsapp-dev-api.nexeagle.com");
            _handler = new GetHospitalQrCodeHandler(_context, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalQrCodeRequestModel { HospitalId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Content, Is.Null);
        }

        [Test]
        public async Task Handle_HospitalHasNoCode_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);

            var response = await _handler.Handle(new GetHospitalQrCodeRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Generate a hospital code first"));
        }

        [Test]
        public async Task Handle_BaseUrlNotConfigured_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            hospital.HospitalCode = "APLO4F";
            await _context.SaveChangesAsync();

            var unconfigured = new GetHospitalQrCodeHandler(_context, new Mock<IConfiguration>().Object);
            var response = await unconfigured.Handle(new GetHospitalQrCodeRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_HospitalWithCode_ReturnsValidPng()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            hospital.HospitalCode = "APLO4F";
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalQrCodeRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ContentType, Is.EqualTo("image/png"));
            Assert.That(response.Content, Is.Not.Null);
            Assert.That(response.Content!.Length, Is.GreaterThan(PngMagicBytes.Length));
            Assert.That(response.Content[..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));
        }
    }
}
