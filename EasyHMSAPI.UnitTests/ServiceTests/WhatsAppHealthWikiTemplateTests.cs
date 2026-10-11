using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class WhatsAppHealthWikiTemplateTests
    {
        private sealed class Meta : HttpMessageHandler
        {
            public int Calls;
            public string? Body;
            public string? Authorization;
            public HttpStatusCode Answer = HttpStatusCode.OK;
            public string Reply = "{}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                Authorization = request.Headers.Authorization?.ToString();
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Answer) { Content = new StringContent(Reply) };
            }
        }

        private static (WhatsAppMessagingService Service, Meta Meta) Make(string enabled = "true", string? language = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["WhatsApp:IsEnabled"] = enabled, ["WhatsApp:ApiUrl"] = "https://graph.example.com/v22.0/123/messages", ["WhatsApp:AccessToken"] = "tok", ["WhatsApp:CountryCode"] = "91",
            };
            if (language != null) settings["HealthWiki:TemplateLanguage"] = language;
            var meta = new Meta();
            return (new WhatsAppMessagingService(new HttpClient(meta), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<WhatsAppMessagingService>.Instance), meta);
        }

        private static List<KeyValuePair<string, string>> P(params (string, string)[] p) => p.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)).ToList();

        [Test]
        public async Task ItSendsTheNamedTemplate_ToTheE164Number_WithNamedBodyVariables()
        {
            var (service, meta) = Make();

            var ok = await service.SendHealthWikiTemplateAsync("9876543210", "health_wiki_review_request", P(("reviewer_name", "Dr. Asha Rao"), ("article_title", "Living with High BP"), ("url", "https://dd.example.com/x")));

            Assert.That(ok, Is.True);
            Assert.That(meta.Authorization, Is.EqualTo("Bearer tok"));
            using var doc = JsonDocument.Parse(meta.Body!);
            var root = doc.RootElement;
            Assert.That(root.GetProperty("to").GetString(), Is.EqualTo("919876543210"));
            Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("template"));
            var template = root.GetProperty("template");
            Assert.That(template.GetProperty("name").GetString(), Is.EqualTo("health_wiki_review_request"));
            Assert.That(template.GetProperty("language").GetProperty("code").GetString(), Is.EqualTo("en"));
            var parameters = template.GetProperty("components")[0].GetProperty("parameters").EnumerateArray().ToList();
            Assert.That(parameters.Select(x => x.GetProperty("parameter_name").GetString()), Is.EqualTo(new[] { "reviewer_name", "article_title", "url" }));
            Assert.That(parameters.Select(x => x.GetProperty("type").GetString()), Is.All.EqualTo("text"));
        }

        [Test]
        public async Task AValueIsFlattenedToOneLine_CutShort_AndNeverEmpty_SoMetaDoesNotRejectIt()
        {
            var (service, meta) = Make();

            await service.SendHealthWikiTemplateAsync("9876543210", "t", P(("comment", "Line one\r\nLine two\tand    spaces"), ("long", new string('a', 1000)), ("empty", "  ")));

            using var doc = JsonDocument.Parse(meta.Body!);
            var values = doc.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters").EnumerateArray()
                .ToDictionary(x => x.GetProperty("parameter_name").GetString()!, x => x.GetProperty("text").GetString()!);
            Assert.That(values["comment"], Is.EqualTo("Line one Line two and spaces"));
            Assert.That(values["long"].Length, Is.EqualTo(300));
            Assert.That(values["empty"], Is.EqualTo("-"));
        }

        [Test]
        public async Task WhenWhatsAppIsSwitchedOff_NothingIsSent()
        {
            var (service, meta) = Make(enabled: "false");
            Assert.That(await service.SendHealthWikiTemplateAsync("9876543210", "t", P(("a", "b"))), Is.False);
            Assert.That(meta.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task WhenMetaRefuses_ItReturnsFalse()
        {
            var (service, meta) = Make();
            meta.Answer = HttpStatusCode.BadRequest;
            Assert.That(await service.SendHealthWikiTemplateAsync("9876543210", "t", P(("a", "b"))), Is.False);
        }

        [Test]
        public async Task TheTemplateLanguageCanBeSet_AsItMustMatchHowTheTemplateWasCreated()
        {
            var (service, meta) = Make(language: "en_US");
            await service.SendHealthWikiTemplateAsync("9876543210", "t", P(("a", "b")));
            using var doc = JsonDocument.Parse(meta.Body!);
            Assert.That(doc.RootElement.GetProperty("template").GetProperty("language").GetProperty("code").GetString(), Is.EqualTo("en_US"));
        }

        [Test]
        public async Task TheInviteUsesTheRenamedTemplate()
        {
            var (service, meta) = Make();
            await service.SendHealthWikiInviteAsync("9876543210", "Dr. Asha", "review the article \"X\"", "https://dd.example.com/contribute/invite/abc");
            using var doc = JsonDocument.Parse(meta.Body!);
            Assert.That(doc.RootElement.GetProperty("template").GetProperty("name").GetString(), Is.EqualTo("health_wiki_invite_1"));
        }

        [Test]
        public async Task TheTrackedSend_ReturnsTheMessageIdWhatsAppGave()
        {
            var (service, meta) = Make();
            meta.Reply = "{\"messaging_product\":\"whatsapp\",\"contacts\":[{\"input\":\"919876543210\",\"wa_id\":\"919876543210\"}],\"messages\":[{\"id\":\"wamid.ABC123\"}]}";

            var r = await service.SendHealthWikiTemplateTrackedAsync("9876543210", "health_wiki_review_request", P(("reviewer_name", "Dr. A"), ("article_title", "T"), ("url", "https://x")));

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.MessageId, Is.EqualTo("wamid.ABC123"));
        }

        [Test]
        public async Task TheTrackedSend_WithAnUnexpectedReplyBody_IsStillAccepted_WithoutAnId()
        {
            var (service, meta) = Make();
            meta.Reply = "not json";

            var r = await service.SendHealthWikiTemplateTrackedAsync("9876543210", "t", P(("url", "https://x")));

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.MessageId, Is.Null);
        }

        [Test]
        public async Task TheTrackedSend_WhenMetaRefuses_IsNotAccepted()
        {
            var (service, meta) = Make();
            meta.Answer = HttpStatusCode.BadRequest;

            var r = await service.SendHealthWikiTemplateTrackedAsync("9876543210", "t", P(("url", "https://x")));

            Assert.That(r.Accepted, Is.False);
            Assert.That(r.MessageId, Is.Null);
        }
    }
}
