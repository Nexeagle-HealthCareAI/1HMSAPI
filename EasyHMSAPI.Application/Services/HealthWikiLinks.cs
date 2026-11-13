namespace EasyHMSAPI.Application.Services
{
    /// <summary>The addresses put in Health Wiki WhatsApp messages. Pure, so the exact paths are tested.</summary>
    public static class HealthWikiLinks
    {
        public static string? Join(string? baseUrl, string path) =>
            string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        // Doctor Dekho: the contributor pages and the public article.
        public static string? ContributorHome(string? doctorDekho) => Join(doctorDekho, "contribute");
        public static string? Write(string? doctorDekho, string slug) => Join(doctorDekho, $"contribute/write/{Uri.EscapeDataString(slug)}");
        public static string? Review(string? doctorDekho, string slug) => Join(doctorDekho, $"contribute/review/{Uri.EscapeDataString(slug)}");
        public static string? Topics(string? doctorDekho) => Join(doctorDekho, "contribute/topics");
        public static string? PublicArticle(string? doctorDekho, string slug) => Join(doctorDekho, $"health/conditions/{Uri.EscapeDataString(slug)}");

        // EasyHMS: a hospital doctor reviews inside the app they already use.
        public static string? EasyHmsReview(string? easyHmsWeb, string slug) => Join(easyHmsWeb, $"health-wiki/{Uri.EscapeDataString(slug)}");
    }
}
