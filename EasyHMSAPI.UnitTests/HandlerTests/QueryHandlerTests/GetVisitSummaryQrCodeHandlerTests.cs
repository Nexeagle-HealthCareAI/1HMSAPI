using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetVisitSummaryQrCodeHandlerTests
    {
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static GetVisitSummaryQrCodeHandler MakeHandler(string? baseUrl = "https://whatsapp-dev-api.nexeagle.com")
        {
            var configurationMock = new Mock<IConfiguration>();
            configurationMock.Setup(c => c["WhatsAppBot:BaseUrl"]).Returns(baseUrl);
            return new GetVisitSummaryQrCodeHandler(configurationMock.Object);
        }

        [Test]
        public async Task Handle_ValidAppointmentId_ReturnsValidPng()
        {
            var handler = MakeHandler();

            var response = await handler.Handle(new GetVisitSummaryQrCodeRequestModel { AppointmentId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Content![..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));
        }

        [Test]
        public async Task Handle_EmptyAppointmentId_ReturnsFailure()
        {
            var handler = MakeHandler();

            var response = await handler.Handle(new GetVisitSummaryQrCodeRequestModel { AppointmentId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_BaseUrlNotConfigured_ReturnsFailure()
        {
            var handler = MakeHandler(baseUrl: null);

            var response = await handler.Handle(new GetVisitSummaryQrCodeRequestModel { AppointmentId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
