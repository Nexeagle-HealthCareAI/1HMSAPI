using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.UnitTests
{
    /// <summary>A stand-in for the Health Wiki's WhatsApp notifier that remembers who was told what, so a test can check it.</summary>
    public class RecordingNotifier : IHealthWikiNotifier
    {
        public record Told(string Code, string Slug, Guid? Person, string? Extra = null, bool ByEditor = false);

        public List<Told> Calls { get; } = new();
        public bool ReminderAccepted { get; set; } = true;

        public IEnumerable<Told> Of(string code) => Calls.FindAll(c => c.Code == code);

        public Task ProfileApprovedAsync(HealthWikiContributor c, CancellationToken ct) { Calls.Add(new Told("N3", "", c.ContributorId)); return Task.CompletedTask; }
        public Task ProfileRejectedAsync(HealthWikiContributor c, CancellationToken ct) { Calls.Add(new Told("N4", "", c.ContributorId, c.RejectReason)); return Task.CompletedTask; }
        public Task RegistrationReceivedAsync(HealthWikiContributor c, CancellationToken ct) { Calls.Add(new Told("N15", "", c.ContributorId)); return Task.CompletedTask; }

        public Task ReviewRequestedAsync(HealthArticleRef a, Guid reviewerId, CancellationToken ct) { Calls.Add(new Told("N5", a.Slug, reviewerId, a.Title)); return Task.CompletedTask; }

        public Task<bool> ReviewReminderAsync(HealthArticleRef a, Guid reviewerId, int days, CancellationToken ct)
        {
            Calls.Add(new Told("N6", a.Slug, reviewerId, $"{a.Title}|{days}"));
            return Task.FromResult(ReminderAccepted);
        }

        public Task ChangesRequestedAsync(HealthArticleRef a, Guid? authorId, string requestedBy, string comment, bool byEditor, CancellationToken ct)
        { Calls.Add(new Told(byEditor ? "N9" : "N7", a.Slug, authorId, $"{requestedBy}|{comment}", byEditor)); return Task.CompletedTask; }

        public Task PublishedAsync(HealthArticleRef a, Guid? authorId, Guid? reviewerId, CancellationToken ct)
        {
            Calls.Add(new Told("N8", a.Slug, authorId, "author"));
            if (reviewerId.HasValue) Calls.Add(new Told("N8", a.Slug, reviewerId, "reviewer"));
            return Task.CompletedTask;
        }

        public Task ArticleRemovedAsync(HealthArticleRef a, Guid? authorId, Guid? reviewerId, string reason, CancellationToken ct)
        {
            Calls.Add(new Told("N14", a.Slug, authorId, reason));
            if (reviewerId.HasValue) Calls.Add(new Told("N14", a.Slug, reviewerId, reason));
            return Task.CompletedTask;
        }

        public Task TopicAcceptedAsync(HealthWikiTopicRequest t, string articleSlug, CancellationToken ct) { Calls.Add(new Told("N10", articleSlug, t.ContributorId, t.Title)); return Task.CompletedTask; }
        public Task TopicDeclinedAsync(HealthWikiTopicRequest t, CancellationToken ct) { Calls.Add(new Told("N11", "", t.ContributorId, t.DecisionNote)); return Task.CompletedTask; }
        public Task TopicNeedsDetailAsync(HealthWikiTopicRequest t, CancellationToken ct) { Calls.Add(new Told("N12", "", t.ContributorId, t.DecisionNote)); return Task.CompletedTask; }
    }
}
