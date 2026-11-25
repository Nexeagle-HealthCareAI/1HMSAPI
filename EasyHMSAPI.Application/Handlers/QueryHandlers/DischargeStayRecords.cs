using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Everything recorded during one admission that the discharge summary is built from — loaded
    /// once and shared by the deterministic draft and the AI features, so both always see the same
    /// records. Also carries the patient's identifiers so AI text can be de-identified.
    /// </summary>
    public sealed class DischargeStayRecords
    {
        public Admission Admission { get; init; } = null!;
        public PatientRegistration? Patient { get; init; }
        public List<RoundNote> RoundNotes { get; init; } = new();
        public List<ClinicalOrderLine> ProcedureLines { get; init; } = new();
        public List<ClinicalOrderLine> ActiveMedicationLines { get; init; } = new();
        public List<ClinicalOrderLine> StoppedMedicationLines { get; init; } = new();
        public List<ClinicalOrderLine> RadiologyLines { get; init; } = new();
        public List<DischargeSummaryComposer.LabResultLine> Labs { get; init; } = new();

        public int ResultedLabCount => Labs.Count(l => DischargeSummaryComposer.FormatResultValues(l.ResultValuesJson) != null || !string.IsNullOrWhiteSpace(l.Interpretation));

        public bool HasAnyRecords =>
            RoundNotes.Count > 0 || ProcedureLines.Count > 0 || ActiveMedicationLines.Count > 0 || StoppedMedicationLines.Count > 0
            || Labs.Count > 0 || RadiologyLines.Count > 0
            || !string.IsNullOrWhiteSpace(Admission.Diagnosis) || !string.IsNullOrWhiteSpace(Admission.AdmissionReason);

        /// <summary>Plain-language list of what was used, shown to the doctor ("6 round notes, 2 procedures, …").</summary>
        public string Summary()
        {
            var parts = new List<string>();
            void Add(int n, string one, string many) { if (n > 0) parts.Add(n + " " + (n == 1 ? one : many)); }
            Add(RoundNotes.Count, "round note", "round notes");
            Add(ProcedureLines.Count, "procedure", "procedures");
            Add(ActiveMedicationLines.Count + StoppedMedicationLines.Count, "medicine order", "medicine orders");
            Add(ResultedLabCount, "lab result", "lab results");
            Add(RadiologyLines.Count, "imaging order", "imaging orders");
            return parts.Count == 0 ? "the admission details" : string.Join(", ", parts);
        }

        public string AgeSex()
        {
            if (Patient == null) return string.Empty;
            var sex = Patient.Sex?.Trim().ToUpperInvariant() switch { "M" or "MALE" => "male", "F" or "FEMALE" => "female", null or "" => null, _ => "other" };
            var unit = Patient.AgeUnit?.Trim().ToUpperInvariant() switch { "M" or "MONTH" or "MONTHS" => "month", "D" or "DAY" or "DAYS" => "day", _ => "year" };
            var age = Patient.Age.HasValue ? $"{Patient.Age}-{unit}-old" : null;
            return string.Join(" ", new[] { age, sex }.Where(s => s != null));
        }

        /// <summary>Removes this patient's names and identifiers from text bound for the AI provider.</summary>
        public string Redact(string? text) => DischargeAiRules.Redact(
            text,
            new[] { Patient?.FullName },
            new[] { Patient?.EmergencyContactName, Patient?.GuardianName },
            new[]
            {
                Admission.PatientId, Admission.AdmissionNo, Admission.AdmissionToken,
                Patient?.Mobile, Patient?.AlternateMobile, Patient?.EmergencyContactPhone,
                Patient?.AadhaarNumber, Patient?.AbhaId, Patient?.Email,
            });

        /// <summary>The de-identified source material for AI drafting.</summary>
        public string AiSourceMaterial()
        {
            var text = DischargeSummaryComposer.ComposeAiSourceMaterial(
                AgeSex(), Admission.AdmittedAt, Admission.ExpectedDischargeAt,
                Admission.Diagnosis, Admission.AdmissionReason,
                RoundNotes, ProcedureLines, Labs, RadiologyLines, ActiveMedicationLines, StoppedMedicationLines);
            var redacted = Redact(text);
            return redacted.Length > DischargeAiRules.MaxSourceLength ? redacted[..DischargeAiRules.MaxSourceLength] : redacted;
        }

        public static async Task<DischargeStayRecords?> LoadAsync(AppDbContext context, Guid hospitalId, Guid admissionId, CancellationToken cancellationToken)
        {
            var admission = await context.Admission
                .FirstOrDefaultAsync(a => a.AdmissionId == admissionId && a.HospitalId == hospitalId, cancellationToken);
            if (admission == null) return null;

            var patient = await context.PatientRegistrations
                .FirstOrDefaultAsync(p => p.HospitalId == hospitalId && p.PatientId == admission.PatientId, cancellationToken);

            var roundNotes = await context.RoundNote
                .Where(r => r.HospitalId == hospitalId && r.AdmissionId == admissionId)
                .OrderBy(r => r.NotedAt)
                .ToListAsync(cancellationToken);

            var orders = await context.ClinicalOrder
                .Where(o => o.HospitalId == hospitalId && o.AdmissionId == admissionId)
                .Select(o => new { o.OrderId, o.OrderType })
                .ToListAsync(cancellationToken);
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var typeByOrder = orders.ToDictionary(o => o.OrderId, o => o.OrderType);
            var lines = orderIds.Count == 0
                ? new List<ClinicalOrderLine>()
                : await context.ClinicalOrderLine
                    .Where(l => l.HospitalId == hospitalId && orderIds.Contains(l.OrderId))
                    .OrderBy(l => l.ScheduledAt ?? l.CreatedAt)
                    .ToListAsync(cancellationToken);

            List<ClinicalOrderLine> OfType(string type) => lines.Where(l => typeByOrder.TryGetValue(l.OrderId, out var t) && t == type).ToList();
            var medication = OfType(IpdConstants.ClinicalOrderType.Medication);
            var labLines = OfType(IpdConstants.ClinicalOrderType.Lab);

            var linkedIds = labLines.Where(l => l.LinkedPathologyOrderLineId.HasValue).Select(l => l.LinkedPathologyOrderLineId!.Value).ToList();
            var results = linkedIds.Count == 0
                ? new List<PathologyResult>()
                : await context.PathologyResult
                    .Where(r => r.HospitalId == hospitalId && linkedIds.Contains(r.OrderLineId))
                    .ToListAsync(cancellationToken);
            var resultByLine = results.GroupBy(r => r.OrderLineId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.UpdatedAt).First());

            var labs = labLines.Select(l =>
            {
                PathologyResult? r = null;
                if (l.LinkedPathologyOrderLineId.HasValue) resultByLine.TryGetValue(l.LinkedPathologyOrderLineId.Value, out r);
                return new DischargeSummaryComposer.LabResultLine(l.ItemName ?? "Lab test", r?.UpdatedAt, r?.ResultValuesJson, r?.Interpretation, r?.HasCriticalFlag ?? false);
            }).ToList();

            return new DischargeStayRecords
            {
                Admission = admission,
                Patient = patient,
                RoundNotes = roundNotes,
                ProcedureLines = OfType(IpdConstants.ClinicalOrderType.Procedure),
                ActiveMedicationLines = medication.Where(l => l.StatusCode == IpdConstants.ClinicalOrderLineStatus.Active).ToList(),
                StoppedMedicationLines = medication.Where(l => l.StatusCode != IpdConstants.ClinicalOrderLineStatus.Active).ToList(),
                RadiologyLines = OfType(IpdConstants.ClinicalOrderType.Radiology),
                Labs = labs,
            };
        }
    }
}
