using System.Text;
using System.Text.RegularExpressions;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Rules for the contributor pages that need no database, so they can be unit-tested on their own.</summary>
    public static class ContributorPortalRules
    {
        public const string ConsentVersion = "2026-10";
        public const int MaxOpenTopics = 5;
        public const int MinRegistrationYear = 1950;

        /// <summary>The three roles the pages offer. A hospital doctor or staff row shows as the closest one.</summary>
        public static string EnrolRole(string contributorType) => contributorType switch
        {
            HealthWikiContributor.TypeHealthWorker => HealthWikiContributor.TypeHealthWorker,
            HealthWikiContributor.TypeWriter => HealthWikiContributor.TypeWriter,
            HealthWikiContributor.TypeIndependentDoctor or HealthWikiContributor.TypeHospitalDoctor => HealthWikiContributor.TypeIndependentDoctor,
            _ => HealthWikiContributor.TypeWriter,
        };

        /// <summary>A profile the team has not rejected and that has been filled in can write and suggest topics.</summary>
        public static bool IsProfileAccepted(string status) =>
            status == HealthWikiContributor.StatusPending || status == HealthWikiContributor.StatusVerified;

        /// <summary>URL-friendly slug from a title: lowercase letters and digits joined by single hyphens, at most 80 chars.</summary>
        public static string Slugify(string? title)
        {
            var sb = new StringBuilder();
            var lastHyphen = true;
            foreach (var ch in (title ?? string.Empty).ToLowerInvariant())
            {
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(ch); lastHyphen = false; }
                else if (!lastHyphen) { sb.Append('-'); lastHyphen = true; }
            }
            var slug = sb.ToString().Trim('-');
            if (slug.Length > 80) slug = slug[..80].Trim('-');
            return slug.Length == 0 ? "article" : slug;
        }

        public static bool TryParseRegistrationYear(string? input, out int year)
        {
            year = 0;
            return int.TryParse(input?.Trim(), out year) && year >= MinRegistrationYear && year <= DateTime.UtcNow.Year;
        }

        /// <summary>"MBBS, MD • Endocrinologist • Reg. 48213 · Delhi Medical Council", kept with a review so the badge text it earned can be shown later.</summary>
        public static string RegistrationLine(HealthWikiContributor c)
        {
            var parts = new[] { c.Qualification, c.Speciality }.Where(p => !string.IsNullOrWhiteSpace(p));
            var line = string.Join(" • ", parts);
            if (!string.IsNullOrWhiteSpace(c.RegistrationNumber))
                line += (line.Length > 0 ? " • " : string.Empty) + $"Reg. {c.RegistrationNumber}" + (string.IsNullOrWhiteSpace(c.RegistrationCouncil) ? "" : $" · {c.RegistrationCouncil}");
            return line.Length > 250 ? line[..250] : line;
        }

        public static bool IsHttpsOrEmpty(string? url) => string.IsNullOrWhiteSpace(url) || HealthArticleRules.IsHttpsUrl(url);
    }
}
