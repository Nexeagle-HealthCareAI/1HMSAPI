using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// The Health Wiki's WhatsApp messages (the product's N1 to N15), each with its own approved template (HealthWikiConstants).
    /// Called AFTER the change it reports has been saved, and it never throws: if WhatsApp is off, a template is not approved yet, a person has
    /// no number, or Meta is slow, the outcome is recorded (HealthWikiNotification, and the article's history) and the real work is untouched.
    /// </summary>
    public interface IHealthWikiNotifier
    {
        /// <summary>N3: the team approved the profile.</summary>
        Task ProfileApprovedAsync(HealthWikiContributor contributor, CancellationToken cancellationToken);

        /// <summary>N4: the team rejected the profile; the reason is on the contributor.</summary>
        Task ProfileRejectedAsync(HealthWikiContributor contributor, CancellationToken cancellationToken);

        /// <summary>N15: the person finished registering and now waits for the team.</summary>
        Task RegistrationReceivedAsync(HealthWikiContributor contributor, CancellationToken cancellationToken);

        /// <summary>N5: an article is with this doctor for review (a hospital doctor is sent to EasyHMS, an independent doctor to Doctor Dekho).</summary>
        Task ReviewRequestedAsync(HealthArticleRef article, Guid reviewerContributorId, CancellationToken cancellationToken);

        /// <summary>N6: the doctor has not decided after a few days. Returns true when WhatsApp accepted it.</summary>
        Task<bool> ReviewReminderAsync(HealthArticleRef article, Guid reviewerContributorId, int days, CancellationToken cancellationToken);

        /// <summary>N7 (a doctor asked for changes) and N9 (the editorial team sent a sector update back): to the author.</summary>
        Task ChangesRequestedAsync(HealthArticleRef article, Guid? authorContributorId, string requestedBy, string comment, bool byEditor, CancellationToken cancellationToken);

        /// <summary>N8 (and N9 when a sector update is approved): the article is live. To the author and the reviewer.</summary>
        Task PublishedAsync(HealthArticleRef article, Guid? authorContributorId, Guid? reviewerContributorId, CancellationToken cancellationToken);

        /// <summary>N14: a live article was taken down. To the author and the reviewer.</summary>
        Task ArticleRemovedAsync(HealthArticleRef article, Guid? authorContributorId, Guid? reviewerContributorId, string reason, CancellationToken cancellationToken);

        /// <summary>N10: the topic was accepted and a draft is waiting.</summary>
        Task TopicAcceptedAsync(HealthWikiTopicRequest topic, string articleSlug, CancellationToken cancellationToken);

        /// <summary>N11: the topic was declined; the reason is on the topic.</summary>
        Task TopicDeclinedAsync(HealthWikiTopicRequest topic, CancellationToken cancellationToken);

        /// <summary>N12: the team needs more detail; what is needed is on the topic.</summary>
        Task TopicNeedsDetailAsync(HealthWikiTopicRequest topic, CancellationToken cancellationToken);
    }
}
