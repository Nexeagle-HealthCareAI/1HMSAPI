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
    public class GetPublicDoctorQrCodeHandlerTests
    {
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static GetPublicDoctorQrCodeHandler MakeHandler(string? baseUrl = "https://whatsapp-dev-api.nexeagle.com")
        {
            var configurationMock = new Mock<IConfiguration>();
            configurationMock.Setup(c => c["WhatsAppBot:BaseUrl"]).Returns(baseUrl);
            return new GetPublicDoctorQrCodeHandler(configurationMock.Object);
        }

        [Test]
        public async Task Handle_ValidDoctorId_ReturnsValidPng_NoDbAccessNeeded()
        {
            var handler = MakeHandler();

            var response = await handler.Handle(new GetPublicDoctorQrCodeRequestModel { DoctorId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Content, Is.Not.Null);
            Assert.That(response.Content![..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));
        }

        [Test]
        public async Task Handle_EmptyDoctorId_ReturnsFailure()
        {
            var handler = MakeHandler();

            var response = await handler.Handle(new GetPublicDoctorQrCodeRequestModel { DoctorId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_BaseUrlNotConfigured_ReturnsFailure()
        {
            var handler = MakeHandler(baseUrl: null);

            var response = await handler.Handle(new GetPublicDoctorQrCodeRequestModel { DoctorId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_DifferentDoctorIds_ProduceDifferentQrContent()
        {
            var handler = MakeHandler();

            var responseA = await handler.Handle(new GetPublicDoctorQrCodeRequestModel { DoctorId = Guid.NewGuid() }, CancellationToken.None);
            var responseB = await handler.Handle(new GetPublicDoctorQrCodeRequestModel { DoctorId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(responseA.Content, Is.Not.EqualTo(responseB.Content));
        }
    }
}
