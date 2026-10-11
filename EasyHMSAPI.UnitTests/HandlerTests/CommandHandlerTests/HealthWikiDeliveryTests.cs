using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HealthWikiDeliveryTests
    {
        private AppDbContext _context = null!;
        private static CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown() => _context.Dispose();

        private HealthWikiNotification Add(string? messageId, string status = HealthWikiNotification.StatusSent, string? delivery = null)
        {
            var n = new HealthWikiNotification
            {
                NotificationId = Guid.NewGuid(), Code = "N8", TemplateName = "health_wiki_article_published", EntityType = "ARTICLE", EntityId = Guid.NewGuid(),
                Status = status, CreatedAt = DateTime.UtcNow, ProviderMessageId = messageId, DeliveryStatus = delivery,
            };
            _context.HealthWikiNotifications.Add(n);
            _context.SaveChanges();
            return n;
        }

        private Task<Application.ResponseModels.CommandResponseModels.RecordHealthWikiDeliveryResponseModel> Record(string? id, string? status, string? detail = null)
            => new RecordHealthWikiDeliveryHandler(_context).Handle(new RecordHealthWikiDeliveryRequestModel { MessageId = id, Status = status, Detail = detail }, Ct);

        [TestCase("delivered", "DELIVERED")]
        [TestCase("READ", "READ")]
        [TestCase(" failed ", "FAILED")]
        [TestCase("sent", null)]
        [TestCase("something-new", null)]
        [TestCase(null, null)]
        public void WhatsAppsStatusWordIsReadAsOurs(string? input, string? expected) => Assert.That(HealthWikiDeliveryRules.Normalize(input), Is.EqualTo(expected));

        [TestCase(null, "DELIVERED", true)]
        [TestCase(null, "READ", true)]
        [TestCase("DELIVERED", "READ", true)]
        [TestCase("READ", "DELIVERED", false)]
        [TestCase("DELIVERED", "DELIVERED", false)]
        [TestCase(null, "FAILED", true)]
        [TestCase("DELIVERED", "FAILED", false)]
        [TestCase("READ", "FAILED", false)]
        [TestCase("FAILED", "DELIVERED", true)]
        public void ALaterCallbackNeverMakesThingsWorse(string? current, string incoming, bool applies) => Assert.That(HealthWikiDeliveryRules.ShouldApply(current, incoming), Is.EqualTo(applies));

        [Test]
        public async Task ADeliveredCallback_IsRecordedOnTheMessageWithThatId()
        {
            var n = Add("wamid.1");
            Add("wamid.2");

            var r = await Record("wamid.1", "delivered");

            Assert.That(r.Matched && r.Applied, Is.True);
            var saved = _context.HealthWikiNotifications.Single(x => x.NotificationId == n.NotificationId);
            Assert.That(saved.DeliveryStatus, Is.EqualTo("DELIVERED"));
            Assert.That(saved.DeliveryAt, Is.Not.Null);
            Assert.That(_context.HealthWikiNotifications.Count(x => x.DeliveryStatus != null), Is.EqualTo(1));
        }

        [Test]
        public async Task AFailedCallback_KeepsWhatsAppsReason()
        {
            var n = Add("wamid.1");

            await Record("wamid.1", "failed", "131049: Meta chose not to deliver this marketing message");

            Assert.That(_context.HealthWikiNotifications.Single(x => x.NotificationId == n.NotificationId).DeliveryDetail, Does.StartWith("131049"));
        }

        [Test]
        public async Task ADeliveredMessageIsNotMarkedFailedByALateCallback()
        {
            var n = Add("wamid.1", delivery: "DELIVERED");

            var r = await Record("wamid.1", "failed", "late");

            Assert.That(r.Applied, Is.False);
            Assert.That(_context.HealthWikiNotifications.Single(x => x.NotificationId == n.NotificationId).DeliveryStatus, Is.EqualTo("DELIVERED"));
        }

        [Test]
        public async Task AnIdFromAnotherFeature_AnswersOkAndChangesNothing()
        {
            Add("wamid.1");

            var r = await Record("wamid.other", "delivered");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Matched, Is.False);
            Assert.That(_context.HealthWikiNotifications.All(x => x.DeliveryStatus == null), Is.True);
        }

        [Test]
        public async Task ASentStatusSaysNothingNew()
        {
            Add("wamid.1");
            var r = await Record("wamid.1", "sent");
            Assert.That(r.Success && !r.Applied, Is.True);
        }

        [Test]
        public async Task AMissingMessageId_IsABadRequest()
        {
            var r = await Record(" ", "delivered");
            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task TheRecordList_CanShowOnlyTheProblems()
        {
            Add("wamid.ok", delivery: "READ");
            Add("wamid.bad", delivery: "FAILED");
            Add(null, status: HealthWikiNotification.StatusSkipped);
            Add("wamid.waiting");

            var all = await new GetHealthWikiNotificationsHandler(_context).Handle(new GetHealthWikiNotificationsRequestModel(), Ct);
            var problems = await new GetHealthWikiNotificationsHandler(_context).Handle(new GetHealthWikiNotificationsRequestModel { ProblemsOnly = true }, Ct);

            Assert.That(all.Notifications, Has.Count.EqualTo(4));
            Assert.That(problems.Notifications, Has.Count.EqualTo(2));
        }
    }
}
