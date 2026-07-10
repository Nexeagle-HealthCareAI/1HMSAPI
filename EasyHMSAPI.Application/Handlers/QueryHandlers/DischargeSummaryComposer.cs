using System.Text;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Pure, DB-free composition of discharge-summary narrative fields from already-loaded
    /// clinical records. Shared by GetDischargeSummaryDraftHandler (deterministic draft) and
    /// GenerateDischargeNarrativeHandler (AI-assist source material) so both use the exact same
    /// underlying source-material assembly — single source of truth.
    /// </summary>
    public static class DischargeSummaryComposer
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        public static string? ComposeCourseInHospital(List<RoundNote> roundNotes)
        {
            if (roundNotes.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var n in roundNotes)
            {
                var dateLabel = (n.NotedAt + IstOffset).ToString("dd MMM yyyy, HH:mm");
                sb.Append(dateLabel).Append(n.IsAddendum ? " (addendum" : "")
                  .Append(n.IsAddendum && !string.IsNullOrWhiteSpace(n.AddendumReason) ? $" — {n.AddendumReason})" : n.IsAddendum ? ")" : "")
                  .Append(':').AppendLine();
                if (!string.IsNullOrWhiteSpace(n.Subjective)) sb.Append("S: ").AppendLine(n.Subjective);
                if (!string.IsNullOrWhiteSpace(n.Objective)) sb.Append("O: ").AppendLine(n.Objective);
                if (!string.IsNullOrWhiteSpace(n.Assessment)) sb.Append("A: ").AppendLine(n.Assessment);
                if (!string.IsNullOrWhiteSpace(n.Plan)) sb.Append("P: ").AppendLine(n.Plan);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static string? ComposeProceduresPerformed(List<ClinicalOrderLine> lines)
        {
            if (lines.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                sb.Append("- ").Append(l.ItemName);
                if (l.ScheduledAt.HasValue) sb.Append(" (").Append((l.ScheduledAt.Value + IstOffset).ToString("dd MMM yyyy")).Append(')');
                if (!string.IsNullOrWhiteSpace(l.Instructions)) sb.Append(" — ").Append(l.Instructions);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static string? ComposeDischargeMedications(List<ClinicalOrderLine> lines)
        {
            if (lines.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                sb.Append("- ").Append(l.ItemName);
                var detail = string.Join(" ", new[] { l.Dose, l.Route, l.Frequency }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(detail)) sb.Append(' ').Append(detail);
                if (l.DurationDays.HasValue) sb.Append(" (" + l.DurationDays.Value + "d)");
                if (!string.IsNullOrWhiteSpace(l.Instructions)) sb.Append(" — ").Append(l.Instructions);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Structured equivalent of ComposeDischargeMedications — one row per active
        /// medication order, for the discharge-medications editor's first-ever (never-saved) draft.
        /// The text-based composer above stays as-is; it's still consumed by
        /// ComposeNarrativeSourceMaterial for the AI-assist prompt.</summary>
        public static List<DischargeMedicationModel> ComposeDischargeMedicationRows(List<ClinicalOrderLine> lines) =>
            lines.Select((l, i) => new DischargeMedicationModel
            {
                MedicineName = l.ItemName,
                Dosage = l.Dose,
                Route = l.Route,
                Frequency = l.Frequency,
                Durations = l.DurationDays.HasValue ? l.DurationDays.Value + "d" : null,
                Instructions = l.Instructions,
                SaltName = l.SaltName,
                DisplayOrder = i,
            }).ToList();

        /// <summary>Formats a structured discharge-medication list back into the same bullet-point
        /// text shape ComposeDischargeMedications produces, so the legacy DischargeSummary.
        /// DischargeMedications column (still read by the AI-narrative prompt / any other text-only
        /// consumer) stays populated once the structured list becomes the source of truth.</summary>
        public static string? ComposeDischargeMedicationsText(
            IEnumerable<(string? Name, string? Dose, string? Route, string? Frequency, string? Duration, string? Instructions)> meds)
        {
            var list = meds.ToList();
            if (list.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var m in list)
            {
                sb.Append("- ").Append(m.Name);
                var detail = string.Join(" ", new[] { m.Dose, m.Route, m.Frequency }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(detail)) sb.Append(' ').Append(detail);
                if (!string.IsNullOrWhiteSpace(m.Duration)) sb.Append(" (" + m.Duration + ")");
                if (!string.IsNullOrWhiteSpace(m.Instructions)) sb.Append(" — ").Append(m.Instructions);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Assembles the full raw source-material block the AI-assist narrative prompt
        /// consumes: diagnosis, round-note timeline, procedures, and active medications, labelled
        /// by section so the model can distinguish them.</summary>
        public static string ComposeNarrativeSourceMaterial(
            string? admittingDiagnosis, string? chiefComplaint,
            List<RoundNote> roundNotes, List<ClinicalOrderLine> procedureLines, List<ClinicalOrderLine> medicationLines)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(admittingDiagnosis)) sb.AppendLine("Admitting diagnosis: " + admittingDiagnosis);
            if (!string.IsNullOrWhiteSpace(chiefComplaint)) sb.AppendLine("Chief complaint: " + chiefComplaint);

            var course = ComposeCourseInHospital(roundNotes);
            if (course != null) sb.AppendLine().AppendLine("Round notes (chronological):").AppendLine(course);

            var procedures = ComposeProceduresPerformed(procedureLines);
            if (procedures != null) sb.AppendLine().AppendLine("Procedures performed:").AppendLine(procedures);

            var medications = ComposeDischargeMedications(medicationLines);
            if (medications != null) sb.AppendLine().AppendLine("Active medications:").AppendLine(medications);

            return sb.ToString().TrimEnd();
        }
    }
}
