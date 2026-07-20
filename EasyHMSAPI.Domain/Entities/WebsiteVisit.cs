using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Page-view beacons fired by NexEagleWebsite (see /public/track-visit) for the CMS "Site
    // Visits" report. Region is resolved server-side from the visitor's real IP
    // (TrustedProxyIpResolver) via a best-effort GeoIP lookup at write time — never trusted from
    // the client.
    [ExcludeFromCodeCoverage]
    [Table("WebsiteVisits")]
    public class WebsiteVisit
    {
        [Key]
        public Guid VisitId { get; set; }
        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

        public string? IpAddress { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }

        public string? PagePath { get; set; }
        public string? ReferrerUrl { get; set; }
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? UtmCampaign { get; set; }

        public string? UserAgent { get; set; }
        // Client-generated (localStorage-persisted) id grouping page views into one visit/session.
        public string? SessionId { get; set; }
    }
}
