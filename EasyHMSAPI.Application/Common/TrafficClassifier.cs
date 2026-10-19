using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Tells people apart from automated traffic in the website-tracking tables (WebsiteVisits,
    /// AnalyticsEvents, HospitalLeads). Crawlers such as Googlebot, Applebot and Meta's link-preview
    /// renderer execute the page's JavaScript, so they fire the same beacons as a visitor and used to
    /// fill the CMS location report with Mountain View, Cupertino, Council Bluffs and the like.
    ///
    /// A row is automated when its user agent is a known bot/tool, or when the geo lookup says the IP
    /// belongs to a hosting provider / data centre (people browse from homes and mobile networks).
    /// The same user-agent list is applied to old rows by 1HMSDatabase
    /// db/schema/migrations/tracking_traffic_quality_2026_10.sql — keep the two in step.
    /// </summary>
    public static class TrafficClassifier
    {
        public const string SiteDoctorDekho = "doctordekho";
        public const string SiteNexEagle = "nexeagle";

        private static readonly Regex BotUserAgent = new(
            @"googlebot|google-inspectiontool|googleother|google-read-aloud|adsbot|mediapartners|apis-google|feedfetcher|google page renderer|storebot|" +
            @"bingbot|bingpreview|msnbot|adidxbot|applebot|facebookexternalhit|facebookcatalog|meta-externalagent|meta-externalfetcher|" +
            @"yandex|baiduspider|duckduckbot|petalbot|bytespider|sogou|ahrefs|semrush|mj12bot|dotbot|dataforseo|seznambot|" +
            @"gptbot|chatgpt-user|oai-searchbot|claudebot|claude-user|perplexity|ccbot|amazonbot|" +
            @"headlesschrome|lighthouse|pagespeed|gtmetrix|pingdom|uptimerobot|statuscake|phantomjs|puppeteer|playwright|selenium|" +
            @"whatsapp|telegrambot|twitterbot|linkedinbot|slackbot|discordbot|skypeuripreview|pinterestbot|redditbot|embedly|vercel-screenshot|" +
            @"python-requests|python-urllib|aiohttp|^curl/|^wget/|okhttp|go-http-client|node-fetch|^axios/|java/|" +
            @"crawler|spider|bot/|bot;|bot\)|\(bot|\+http",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(50));

        /// <summary>True for a missing user agent or one that names a bot, crawler, preview renderer or HTTP library.</summary>
        public static bool IsAutomatedUserAgent(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return true;   // every real browser sends one
            try
            {
                return BotUserAgent.IsMatch(userAgent);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;   // pathological input: count it rather than drop a possible visitor
            }
        }

        /// <summary>'doctordekho' | 'nexeagle', or null for anything else.</summary>
        public static string? NormalizeSite(string? site) =>
            site?.Trim().ToLowerInvariant() switch
            {
                SiteDoctorDekho => SiteDoctorDekho,
                SiteNexEagle => SiteNexEagle,
                _ => null,
            };
    }
}
