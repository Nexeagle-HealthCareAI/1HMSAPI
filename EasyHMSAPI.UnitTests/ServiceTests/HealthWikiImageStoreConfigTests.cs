using System.Collections.Generic;
using EasyHMSAPI.Application.Services.Implementations;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class HealthWikiImageStoreConfigTests
    {
        private static HealthWikiImageStore Store(params (string Key, string? Value)[] settings)
        {
            var d = new Dictionary<string, string?>();
            foreach (var (k, v) in settings) d[k] = v;
            return new HealthWikiImageStore(new ConfigurationBuilder().AddInMemoryCollection(d).Build());
        }

        private const string Host = "https://nexeagle-dev.in-south1-objectstore.e2enetworks.net";

        [Test]
        public void WithItsOwnKey_ItWorksEvenWhenTheSharedKeyIsMissing() =>
            Assert.That(Store(("Storage:S3:ServiceUrl", Host), ("Storage:S3:Bucket", "nexeagle-dev"), ("HealthWiki:ImageBucket", "nexeagle-healthwiki-dev"),
                ("HealthWiki:ImageAccessKey", "AKEY"), ("HealthWiki:ImageSecretKey", "ASECRET")).IsConfigured, Is.True);

        [Test]
        public void WithoutItsOwnKey_ItFallsBackToTheSharedKey() =>
            Assert.That(Store(("Storage:S3:ServiceUrl", Host), ("Storage:S3:Bucket", "nexeagle-dev"), ("Storage:S3:AccessKey", "SHARED"), ("Storage:S3:SecretKey", "SHAREDSECRET"),
                ("HealthWiki:ImageBucket", "nexeagle-healthwiki-dev")).IsConfigured, Is.True);

        [Test]
        public void ThePlaceholderTextIsNeverAKey_SoItIsNotConfigured() =>
            Assert.That(Store(("Storage:S3:ServiceUrl", Host), ("Storage:S3:Bucket", "nexeagle-dev"), ("HealthWiki:ImageBucket", "nexeagle-healthwiki-dev"),
                ("HealthWiki:ImageAccessKey", "<set-via-env-HealthWiki-ImageAccessKey>"), ("HealthWiki:ImageSecretKey", "<set-via-env-HealthWiki-ImageSecretKey>")).IsConfigured, Is.False);

        [Test]
        public void HalfAKeyIsNotAKey_AndNoBucketMeansNotConfigured()
        {
            Assert.That(Store(("Storage:S3:ServiceUrl", Host), ("HealthWiki:ImageBucket", "b"), ("HealthWiki:ImageAccessKey", "AKEY")).IsConfigured, Is.False);
            Assert.That(Store(("Storage:S3:ServiceUrl", Host), ("HealthWiki:ImageAccessKey", "AKEY"), ("HealthWiki:ImageSecretKey", "S")).IsConfigured, Is.False);
        }
    }
}
