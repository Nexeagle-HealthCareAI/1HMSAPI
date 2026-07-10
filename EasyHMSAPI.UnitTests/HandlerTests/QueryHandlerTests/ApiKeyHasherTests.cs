using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class ApiKeyHasherTests
    {
        [Test]
        public void Hash_SameInput_ProducesSameHash()
        {
            var hash1 = ApiKeyHasher.Hash("my-raw-key");
            var hash2 = ApiKeyHasher.Hash("my-raw-key");

            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void Hash_DifferentInput_ProducesDifferentHash()
        {
            var hash1 = ApiKeyHasher.Hash("key-one");
            var hash2 = ApiKeyHasher.Hash("key-two");

            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void GenerateRawKey_ProducesUniqueNonEmptyKeys()
        {
            var key1 = ApiKeyHasher.GenerateRawKey();
            var key2 = ApiKeyHasher.GenerateRawKey();

            Assert.That(key1, Is.Not.Empty);
            Assert.That(key1, Does.StartWith("nxk_"));
            Assert.That(key1, Is.Not.EqualTo(key2));
        }
    }
}
