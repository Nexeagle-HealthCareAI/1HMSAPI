using System.Text;
using System.Text.Json;
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

        /// <summary>One lab test of this stay with its result, if any (from the linked pathology result).</summary>
        public sealed record LabResultLine(string TestName, DateTime? ResultAt, string? ResultValuesJson, string? Interpretation, bool HasCriticalFlag);

        /// <summary>Flattens a pathology ResultValuesJson into "Hemoglobin 8.2 (LOW), WBC 11000".
        /// Handles both the flagged shape {"Hb":{"value":"8.2","flag":"LOW"}} and the older {"Hb":"14.2"}.</summary>
        public static string? FormatResultValues(string? resultValuesJson)
        {
            if (string.IsNullOrWhiteSpace(resultValuesJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(resultValuesJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                var parts = new List<string>();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    string? value = null, flag = null;
                    if (p.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (p.Value.TryGetProperty("value", out var v)) value = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
                        if (p.Value.TryGetProperty("flag", out var f) && f.ValueKind == JsonValueKind.String) flag = f.GetString();
                    }
                    else if (p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    {
                        value = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                    }
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var showFlag = !string.IsNullOrWhiteSpace(flag) && !string.Equals(flag, "NORMAL", StringComparison.OrdinalIgnoreCase);
                    parts.Add(p.Name + " " + value!.Trim() + (showFlag ? " (" + flag!.Replace('_', ' ') + ")" : ""));
                }
                return parts.Count == 0 ? null : string.Join(", ", parts);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Deterministic "Key investigations": one line per resulted lab test, oldest first.
        /// Tests without a result yet are left out (they are not findings).</summary>
        public static string? ComposeKeyInvestigations(List<LabResultLine> labs)
        {
            var lines = new List<string>();
            foreach (var l in labs.OrderBy(l => l.ResultAt ?? DateTime.MaxValue))
            {
                var values = FormatResultValues(l.ResultValuesJson);
                if (values == null && string.IsNullOrWhiteSpace(l.Interpretation)) continue;
                var sb = new StringBuilder("- ").Append(l.TestName);
                if (l.ResultAt.HasValue) sb.Append(" (").Append((l.ResultAt.Value + IstOffset).ToString("dd MMM")).Append(')');
                if (values != null) sb.Append(": ").Append(values);
                if (!string.IsNullOrWhiteSpace(l.Interpretation)) sb.Append(values != null ? " — " : ": ").Append(l.Interpretation!.Trim());
                if (l.HasCriticalFlag) sb.Append(" [critical value]");
                lines.Add(sb.ToString());
            }
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>
        /// Everything the AI may see about one stay, labelled by section: who (age/sex only), when,
        /// admission diagnosis and reason, round notes, procedures, investigations, imaging ordered,
        /// and medicines (active and stopped). Callers must run the result through
        /// DischargeAiRules.Redact before sending it anywhere.
        /// </summary>
        public static string ComposeAiSourceMaterial(
            string? ageSex, DateTime admittedAt, DateTime? expectedDischargeAt,
            string? admissionDiagnosis, string? admissionReason,
            List<RoundNote> roundNotes, List<ClinicalOrderLine> procedureLines, List<LabResultLine> labs,
            List<ClinicalOrderLine> radiologyLines, List<ClinicalOrderLine> activeMedicationLines, List<ClinicalOrderLine> stoppedMedicationLines)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(ageSex)) sb.AppendLine("Patient: " + ageSex);
            var stayDays = Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - admittedAt).TotalDays));
            sb.AppendLine("Admitted: " + (admittedAt + IstOffset).ToString("dd MMM yyyy") + " (day " + stayDays + " of stay)");
            if (expectedDischargeAt.HasValue) sb.AppendLine("Expected discharge: " + (expectedDischargeAt.Value + IstOffset).ToString("dd MMM yyyy"));
            if (!string.IsNullOrWhiteSpace(admissionDiagnosis)) sb.AppendLine("Diagnosis at admission: " + admissionDiagnosis);
            if (!string.IsNullOrWhiteSpace(admissionReason)) sb.AppendLine("Reason for admission / complaint: " + admissionReason);

            var course = ComposeCourseInHospital(roundNotes);
            if (course != null) sb.AppendLine().AppendLine("Round notes (chronological):").AppendLine(course);

            var procedures = ComposeProceduresPerformed(procedureLines);
            if (procedures != null) sb.AppendLine().AppendLine("Procedures:").AppendLine(procedures);

            var investigations = ComposeKeyInvestigations(labs);
            if (investigations != null) sb.AppendLine().AppendLine("Lab results:").AppendLine(investigations);
            var pending = labs.Where(l => FormatResultValues(l.ResultValuesJson) == null && string.IsNullOrWhiteSpace(l.Interpretation)).Select(l => l.TestName).Distinct().ToList();
            if (pending.Count > 0) sb.AppendLine("Lab tests ordered without a result yet: " + string.Join(", ", pending));

            if (radiologyLines.Count > 0)
                sb.AppendLine().AppendLine("Imaging ordered (reports are not in these records): " + string.Join(", ", radiologyLines.Select(l => l.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()));

            var meds = ComposeDischargeMedications(activeMedicationLines);
            if (meds != null) sb.AppendLine().AppendLine("Medicines still active:").AppendLine(meds);
            var stopped = ComposeDischargeMedications(stoppedMedicationLines);
            if (stopped != null) sb.AppendLine().AppendLine("Medicines given earlier and stopped:").AppendLine(stopped);

            return sb.ToString().TrimEnd();
        }

        /// <summary>Marks every section a fresh draft filled from records as RECORDS (JSON map).</summary>
        public static string? ComposeRecordSources(DischargeSummaryDraftModel draft)
        {
            var map = new Dictionary<string, string>();
            void Mark(string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) map[key] = "RECORDS"; }
            Mark("admittingDiagnosis", draft.AdmittingDiagnosis);
            Mark("chiefComplaint", draft.ChiefComplaint);
            Mark("courseInHospital", draft.CourseInHospital);
            Mark("proceduresPerformed", draft.ProceduresPerformed);
            Mark("keyInvestigations", draft.KeyInvestigations);
            if (draft.Medications.Count > 0) map["dischargeMedications"] = "RECORDS";
            return map.Count == 0 ? null : JsonSerializer.Serialize(map);
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
