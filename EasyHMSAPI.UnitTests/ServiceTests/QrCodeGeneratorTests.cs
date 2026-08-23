using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class QrCodeGeneratorTests
    {
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        [Test]
        public void GenerateWithLogo_ReturnsValidPng()
        {
            var bytes = QrCodeGenerator.GenerateWithLogo("https://whatsapp-dev-api.nexeagle.com/c/APLO4F");

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(PngMagicBytes.Length));
            Assert.That(bytes[..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));
        }

        [Test]
        public void GenerateWithLogo_LongerData_StillProducesValidPng()
        {
            // Confirms the ECC level / module sizing holds up for a longer URL, not just a
            // short example -- a real check-in URL could be longer than the "APLO4F" example.
            var bytes = QrCodeGenerator.GenerateWithLogo("https://whatsapp-dev-api.nexeagle.com/c/SOMEMUCHLONGERHOSPITALCODE1234567890");

            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes[..PngMagicBytes.Length], Is.EqualTo(PngMagicBytes));
        }

        [Test]
        public void GenerateWithLogo_IsDeterministic_SameInputSameOutput()
        {
            var first = QrCodeGenerator.GenerateWithLogo("https://whatsapp-dev-api.nexeagle.com/c/APLO4F");
            var second = QrCodeGenerator.GenerateWithLogo("https://whatsapp-dev-api.nexeagle.com/c/APLO4F");

            Assert.That(first, Is.EqualTo(second));
        }
    }
}
