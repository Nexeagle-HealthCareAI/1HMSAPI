using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class TextColourValidatorTests
    {
        [TestCase("#111827")]
        [TestCase("#FFFFFF")]
        [TestCase("#000000")]
        [TestCase("#111827FF")]
        [TestCase("#abcdef")]
        public void IsValid_WellFormedHex_ReturnsTrue(string colour)
        {
            Assert.That(TextColourValidator.IsValid(colour), Is.True);
        }

        [TestCase("red")]
        [TestCase("111827")]
        [TestCase("#11182")]
        [TestCase("#1118277")]
        [TestCase("#GGGGGG")]
        [TestCase("rgb(17,24,39)")]
        [TestCase("")]
        [TestCase("#")]
        public void IsValid_MalformedValue_ReturnsFalse(string colour)
        {
            Assert.That(TextColourValidator.IsValid(colour), Is.False);
        }
    }
}
