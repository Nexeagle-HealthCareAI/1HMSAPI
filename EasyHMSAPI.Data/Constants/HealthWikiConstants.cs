using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Data.Constants
{
    /// <summary>
    /// Health Wiki WhatsApp messages: the product's message ids (N1 to N15) and the names of the Meta-approved templates that carry them.
    /// A template renamed or re-created in WhatsApp Manager is changed here, in one place. Each template's variable names are
    /// listed with it, because Meta matches them by name.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class HealthWikiConstants
    {
        public static class Template
        {
            // contributor_name, task, url
            public const string Invite = "health_wiki_invite_1";
            // contributor_name, url
            public const string ProfileApproved = "health_wiki_profile_approved";
            // contributor_name, reason
            public const string ProfileRejected = "health_wiki_profile_rejected";
            // reviewer_name, article_title, url
            public const string ReviewRequest = "health_wiki_review_request";
            // reviewer_name, article_title, days, url
            public const string ReviewReminder = "health_wiki_review_reminder";
            // author_name, requested_by, article_title, comment, url
            public const string ChangesRequested = "health_wiki_changes_requested";
            // person_name, article_title, url
            public const string ArticlePublished = "health_wiki_article_published";
            // contributor_name, topic_title, url
            public const string TopicAccepted = "health_wiki_topic_accepted";
            // contributor_name, topic_title, reason
            public const string TopicDeclined = "health_wiki_topic_declined";
            // contributor_name, topic_title, request, url
            public const string TopicNeedsDetail = "health_wiki_topic_needs_detail";
            // person_name, article_title, reason
            public const string ArticleRemoved = "health_wiki_article_removed";
            // contributor_name
            public const string RegistrationReceived = "health_wiki_registration_received";
        }

        // N2 (the sign-in code) uses the shared "otp" template and N13 (admins) is shown inside the CMS, so neither is here.
        public static class Code
        {
            public const string Invite = "N1";
            public const string ProfileApproved = "N3";
            public const string ProfileRejected = "N4";
            public const string ReviewRequest = "N5";
            public const string ReviewReminder = "N6";
            public const string ChangesRequested = "N7";
            public const string ArticlePublished = "N8";
            public const string ReturnedByEditor = "N9";
            public const string TopicAccepted = "N10";
            public const string TopicDeclined = "N11";
            public const string TopicNeedsDetail = "N12";
            public const string ArticleRemoved = "N14";
            public const string RegistrationReceived = "N15";
        }
    }
}
