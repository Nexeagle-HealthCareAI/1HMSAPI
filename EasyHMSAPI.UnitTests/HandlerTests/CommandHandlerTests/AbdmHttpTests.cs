using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Locks in the abdm-m1 skill's transport rules: ISO-ms timestamps, a fresh REQUEST-ID per call,
    // tolerant error-code matching, empty-body failures, and a single (never looped) 401 retry.
    [TestFixture]
    public class AbdmHttpTests
    {
        [Test]
        public void Timestamp_IsUtcWithMillisecondsAndZ()
        {
            var value = AbdmHttp.FormatTimestamp(new DateTime(2022, 10, 6, 15, 10, 0, 587, DateTimeKind.Utc).AddTicks(1234));
            Assert.That(value, Is.EqualTo("2022-10-06T15:10:00.587Z"));
        }

        [Test]
        public void StandardHeaders_AreAllPresent_AndRequestIdIsFreshEachCall()
        {
            var first = new HttpRequestMessage(HttpMethod.Get, "https://example.test/x");
            var second = new HttpRequestMessage(HttpMethod.Get, "https://example.test/x");
            var id1 = AbdmHttp.AddStandardHeaders(first, "sbx");
            var id2 = AbdmHttp.AddStandardHeaders(second, "sbx");

            Assert.That(id1, Is.Not.EqualTo(id2));
            Assert.That(first.Headers.GetValues("REQUEST-ID").Single(), Is.EqualTo(id1));
            Assert.That(first.Headers.GetValues("X-CM-ID").Single(), Is.EqualTo("sbx"));
            Assert.That(first.Headers.GetValues("TIMESTAMP").Single(), Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"));
        }

        [TestCase("ABDM-1114", "ABDM-1114")]
        [TestCase("ABDM-1114: ", "ABDM-1114")]
        [TestCase("abdm-1114:", "ABDM-1114")]
        [TestCase("  900901 : ", "900901")]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void NormaliseCode_ToleratesTheSeparator(string? raw, string? expected)
        {
            Assert.That(AbdmHttp.NormaliseCode(raw), Is.EqualTo(expected));
        }

        [Test]
        public void ParseError_CodeAndMessageAtRoot()
        {
            var (code, message) = AbdmHttp.ParseError("{\"code\":\"ABDM-1114: \",\"message\":\"ABDM-1114: No account found\"}");
            Assert.That(code, Is.EqualTo("ABDM-1114"));
            Assert.That(message, Is.EqualTo("No account found"));
        }

        [Test]
        public void ParseError_NestedErrorObject()
        {
            var (code, message) = AbdmHttp.ParseError("{\"error\":{\"code\":\"ABDM-1003\",\"message\":\"Invalid OTP value\"}}");
            Assert.That(code, Is.EqualTo("ABDM-1003"));
            Assert.That(message, Is.EqualTo("Invalid OTP value"));
        }

        [Test]
        public void ParseError_FieldMapNamesTheField()
        {
            var (code, message) = AbdmHttp.ParseError("{\"loginId\":\"Invalid LoginId\",\"timestamp\":\"2024-01-01\"}");
            Assert.That(code, Is.Null);
            Assert.That(message, Is.EqualTo("loginId: Invalid LoginId"));
        }

        [Test]
        public void ParseError_TopLevelArray()
        {
            var (code, _) = AbdmHttp.ParseError("[{\"code\":\"ABDM-1017\",\"message\":\"x\"}]");
            Assert.That(code, Is.EqualTo("ABDM-1017"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void ParseError_EmptyBody_DoesNotThrow(string? body)
        {
            var (code, message) = AbdmHttp.ParseError(body);
            Assert.That(code, Is.Null);
            Assert.That(message, Is.Null);
        }

        [Test]
        public void ParseError_HtmlFromAProxy_IsNotShownToTheUser()
        {
            var (_, message) = AbdmHttp.ParseError("<html><body>502 Bad Gateway</body></html>");
            Assert.That(message, Is.Null);
        }

        [Test]
        public void UserMessage_NeverContainsTheUrlOrRawBody_AndCarriesTheReference()
        {
            var text = AbdmHttp.BuildUserMessage("ABDM-1114", null, 400, "req-1");
            Assert.That(text, Does.Contain("ABDM-1114"));
            Assert.That(text, Does.Contain("req-1"));
            Assert.That(text, Does.Not.Contain("http"));
        }

        [Test]
        public void HasCode_MatchesWithOrWithoutSeparator()
        {
            var ex = new AbdmException("x", AbdmHttp.NormaliseCode("ABDM-1114: "), 400, "r");
            Assert.That(ex.HasCode("ABDM-1114"), Is.True);
            Assert.That(ex.HasCode("ABDM-1114: "), Is.True);
            Assert.That(ex.HasCode("ABDM-1115"), Is.False);
        }

        // ---------------------------------------------------------------- SendAsync

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> _responses;
            public int Calls { get; private set; }
            public StubHandler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(_responses.Dequeue());
            }
        }

        private static HttpResponseMessage Respond(HttpStatusCode status, string body = "") =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static Task<HttpRequestMessage> Build()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://abhasbx.abdm.gov.in/abha/api/v3/profile/account");
            AbdmHttp.AddStandardHeaders(request, "sbx");
            return Task.FromResult(request);
        }

        [Test]
        public async Task Send_On401_DropsTheTokenAndRetriesOnce()
        {
            var handler = new StubHandler(Respond(HttpStatusCode.Unauthorized), Respond(HttpStatusCode.OK, "{\"ok\":true}"));
            var gateway = new Mock<IAbdmGatewayService>();

            var response = await AbdmHttp.SendAsync(new HttpClient(handler), gateway.Object, NullLogger.Instance, Build, CancellationToken.None);

            Assert.That(handler.Calls, Is.EqualTo(2));
            gateway.Verify(g => g.InvalidateAccessToken(), Times.Once);
            Assert.That(response.BodyText, Does.Contain("ok"));
        }

        [Test]
        public void Send_TwoConsecutive401s_StopsAfterOneRetry()
        {
            var handler = new StubHandler(Respond(HttpStatusCode.Unauthorized), Respond(HttpStatusCode.Unauthorized), Respond(HttpStatusCode.OK));
            var gateway = new Mock<IAbdmGatewayService>();

            var ex = Assert.ThrowsAsync<AbdmException>(() =>
                AbdmHttp.SendAsync(new HttpClient(handler), gateway.Object, NullLogger.Instance, Build, CancellationToken.None));

            Assert.That(handler.Calls, Is.EqualTo(2));
            Assert.That(ex!.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public void Send_RefusalWithEmptyBody_ThrowsASafeMessage()
        {
            var handler = new StubHandler(Respond(HttpStatusCode.BadRequest));
            var gateway = new Mock<IAbdmGatewayService>();

            var ex = Assert.ThrowsAsync<AbdmException>(() =>
                AbdmHttp.SendAsync(new HttpClient(handler), gateway.Object, NullLogger.Instance, Build, CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("HTTP 400"));
            Assert.That(ex.Message, Does.Not.Contain("abhasbx"));
            Assert.That(ex.RequestId, Is.Not.Empty);
            gateway.Verify(g => g.InvalidateAccessToken(), Times.Never);
        }

        [Test]
        public void Send_RefusalWithCode_IsCatchableAsInvalidOperationException()
        {
            var handler = new StubHandler(Respond(HttpStatusCode.BadRequest, "{\"code\":\"ABDM-1114: \",\"message\":\"No ABHA found\"}"));

            var ex = Assert.ThrowsAsync<AbdmException>(() =>
                AbdmHttp.SendAsync(new HttpClient(handler), new Mock<IAbdmGatewayService>().Object, NullLogger.Instance, Build, CancellationToken.None));

            Assert.That(ex, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex!.HasCode("ABDM-1114"), Is.True);
            Assert.That(ex.Message, Does.StartWith("No ABHA found"));
        }
    }
}
