using EasyHMSAPI.Application.Common;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.CommonTests
{
    [TestFixture]
    public class TrafficClassifierTests
    {
        [TestCase("Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.6533.119 Mobile Safari/537.36 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
        [TestCase("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.1.1 Safari/605.1.15 (Applebot/0.1; +http://www.apple.com/go/applebot)")]
        [TestCase("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)")]
        [TestCase("meta-externalagent/1.1 (+https://developers.facebook.com/docs/sharing/webmasters/crawler)")]
        [TestCase("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/128.0.0.0 Safari/537.36")]
        [TestCase("Mozilla/5.0 (Linux; Android 11; moto g power (2022)) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Mobile Safari/537.36 Chrome-Lighthouse")]
        [TestCase("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm) Chrome/116.0.1938.76 Safari/537.36")]
        [TestCase("Mozilla/5.0 (compatible; Google-InspectionTool/1.0;)")]
        [TestCase("python-requests/2.32.3")]
        [TestCase("curl/8.7.1")]
        [TestCase("")]
        [TestCase(null)]
        public void Bots_AreAutomated(string? ua) => Assert.That(TrafficClassifier.IsAutomatedUserAgent(ua), Is.True);

        [TestCase("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36")]
        [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 17_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.6 Mobile/15E148 Safari/604.1")]
        [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 17_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 [FBAN/FBIOS;FBAV/480.0.0.0]")]
        [TestCase("Mozilla/5.0 (Linux; Android 9; CUBOT_X19) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36")]
        [TestCase("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.0.0")]
        public void People_AreNotAutomated(string ua) => Assert.That(TrafficClassifier.IsAutomatedUserAgent(ua), Is.False);

        [TestCase("doctordekho", "doctordekho")]
        [TestCase(" NexEagle ", "nexeagle")]
        [TestCase("other", null)]
        [TestCase(null, null)]
        public void NormalizeSite_AcceptsOnlyKnownSites(string? input, string? expected) =>
            Assert.That(TrafficClassifier.NormalizeSite(input), Is.EqualTo(expected));
    }
}
