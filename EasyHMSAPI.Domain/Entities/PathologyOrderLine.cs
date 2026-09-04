using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class PathologyOrderLine
    {
        [Key]
        public Guid OrderLineId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
        public Guid TestId { get; set; }
        
        // Status: PENDING, SAMPLE_COLLECTED, RESULT_ENTERED, and (outsourced lines only)
        // SENT_TO_EXTERNAL_LAB, RESULT_RECEIVED_FROM_EXTERNAL_LAB between the two -- see
        // SendPathologyLineToExternalLabHandler / ReceivePathologyExternalLabResultHandler.
        // RESULT_ENTERED is terminal -- there is no further approval step (the sign-off workflow was
        // removed; a report can be freely generated/regenerated from a line's results at any point
        // after they're entered).
        public string Status { get; set; } = "PENDING";

        public string? SampleBarcode { get; set; }
        public DateTime? SampleCollectedAt { get; set; }

        // Set only for a line whose test is outsourced (PathologyTestMaster.IsOutsourced). ExternalLabId
        // is a soft link (no FK) to PathologyExternalLab, defaulted from the test's
        // DefaultExternalLabId at send time but editable per line. ExternalLabCost snapshots
        // PathologyTestMaster.CostPrice at send time so a later catalog cost edit can't retroactively
        // change an already-sent line's recorded cost.
        public Guid? ExternalLabId { get; set; }
        public DateTime? SentToExternalLabAt { get; set; }
        public string? ExternalLabRefNo { get; set; }
        public DateTime? ExternalLabReceivedAt { get; set; }
        public decimal? ExternalLabCost { get; set; }

        public Guid? ReportId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
