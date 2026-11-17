using System.Text.Json;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Fhir
{
    /// <summary>One record ready to send: its ABDM type and the FHIR bundle as JSON.</summary>
    public sealed record AbdmBuiltRecord(string HiType, string Json);

    public sealed class AbdmBuiltRecords
    {
        /// <summary>When the visit or admission happened; a consent's date range is checked against it.</summary>
        public DateTime WhenUtc { get; init; }
        public IReadOnlyList<AbdmBuiltRecord> Records { get; init; } = Array.Empty<AbdmBuiltRecord>();
    }

    public interface IAbdmRecordSource
    {
        /// <summary>Builds the FHIR bundles for one care context, limited to the HI types allowed.
        /// Null when the visit behind the care context no longer exists.</summary>
        Task<AbdmBuiltRecords?> BuildAsync(AbdmCareContext careContext, IReadOnlyCollection<string> allowedHiTypes, CancellationToken cancellationToken);
    }

    /// <summary>Reads a visit's or an admission's records and hands them to <see cref="AbdmFhir"/>.</summary>
    public sealed class AbdmRecordSource : IAbdmRecordSource
    {
        private readonly AppDbContext _db;

        public AbdmRecordSource(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AbdmBuiltRecords?> BuildAsync(AbdmCareContext careContext, IReadOnlyCollection<string> allowedHiTypes, CancellationToken cancellationToken)
        {
            var hospitalId = careContext.HospitalId;
            bool Allowed(string hiType) => allowedHiTypes.Contains(hiType, StringComparer.OrdinalIgnoreCase);

            var registration = await _db.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == hospitalId && p.PatientId == careContext.PatientId)
                .Select(p => new { p.FullName, p.Sex, p.DateOfBirth })
                .FirstOrDefaultAsync(cancellationToken);
            var patient = new FhirPatient(careContext.PatientId, registration?.FullName, registration?.Sex, registration?.DateOfBirth, careContext.AbhaNumber, careContext.AbhaAddress);

            var hospitalName = await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == hospitalId).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
            var hipId = await _db.AbdmFacilities.AsNoTracking().Where(f => f.HospitalId == hospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
            var facility = new FhirFacility(hospitalId.ToString(), string.IsNullOrWhiteSpace(hospitalName) ? "Hospital" : hospitalName!, hipId);

            var records = new List<AbdmBuiltRecord>();
            void Add(string hiType, System.Text.Json.Nodes.JsonObject bundle)
            {
                if (AbdmFhir.HasContent(bundle))
                    records.Add(new AbdmBuiltRecord(hiType, bundle.ToJsonString()));
            }

            if (careContext.SourceType == AbdmCareContextSource.Opd)
            {
                var appointment = await _db.Appointments.AsNoTracking()
                    .Where(a => a.ApptId == careContext.SourceId && a.HospitalId == hospitalId)
                    .Select(a => new { a.ApptDate, a.DoctorId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (appointment == null)
                    return null;
                var context = new FhirContext(patient, await AuthorAsync(appointment.DoctorId, null, cancellationToken), facility, appointment.ApptDate, careContext.ReferenceNumber);

                if (Allowed("Prescription"))
                {
                    var prescription = await _db.Prescription.AsNoTracking()
                        .Where(p => p.ApptId == careContext.SourceId && p.HospitalId == hospitalId)
                        .OrderByDescending(p => p.UpdatedAt)
                        .Select(p => new { p.PrescriptionId, p.Diagnosis })
                        .FirstOrDefaultAsync(cancellationToken);
                    if (prescription != null)
                    {
                        var medicines = await _db.PrescriptionMedicine.AsNoTracking()
                            .Where(m => m.PrescriptionId == prescription.PrescriptionId)
                            .OrderBy(m => m.DisplayOrder)
                            .Select(m => new FhirMedicine(m.MedicineName, m.Dosage, m.Frequency, m.Durations, m.Route, m.Instructions))
                            .ToListAsync(cancellationToken);
                        Add("Prescription", AbdmFhir.Prescription(context, medicines, prescription.Diagnosis));
                    }
                }

                if (Allowed("WellnessRecord"))
                {
                    var vitalsJson = await _db.AppointmentVitals.AsNoTracking()
                        .Where(v => v.ApptId == careContext.SourceId && v.HospitalId == hospitalId)
                        .OrderByDescending(v => v.RecordedAt)
                        .Select(v => v.VitalsJson)
                        .FirstOrDefaultAsync(cancellationToken);
                    var vitals = ParseVitals(vitalsJson);
                    if (vitals != null && !vitals.IsEmpty)
                        Add("WellnessRecord", AbdmFhir.WellnessRecord(context, vitals));
                }

                if (Allowed("DiagnosticReport"))
                {
                    var encounterIds = await _db.Encounter.AsNoTracking()
                        .Where(e => e.HospitalId == hospitalId && e.SourceId == careContext.SourceId)
                        .Select(e => (Guid?)e.EncounterId)
                        .ToListAsync(cancellationToken);
                    if (encounterIds.Count > 0)
                    {
                        var orderIds = await _db.PathologyOrder.AsNoTracking()
                            .Where(o => o.HospitalId == hospitalId && encounterIds.Contains(o.EncounterId))
                            .Select(o => o.OrderId)
                            .ToListAsync(cancellationToken);
                        await AddLabReportsAsync(context, hospitalId, orderIds, Add, cancellationToken);
                    }
                }
                return new AbdmBuiltRecords { WhenUtc = appointment.ApptDate, Records = records };
            }

            var admission = await _db.Admission.AsNoTracking()
                .Where(a => a.AdmissionId == careContext.SourceId && a.HospitalId == hospitalId)
                .Select(a => new { a.AdmittedAt, a.PrimaryDoctorId })
                .FirstOrDefaultAsync(cancellationToken);
            if (admission == null)
                return null;

            var discharge = await _db.DischargeSummary.AsNoTracking()
                .Where(d => d.AdmissionId == careContext.SourceId && d.HospitalId == hospitalId && d.SignedAt != null)
                .OrderByDescending(d => d.SignedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var ipdContext = new FhirContext(patient,
                await AuthorAsync(discharge?.SignedByDoctorId ?? admission.PrimaryDoctorId, discharge?.SignedByDoctorName, cancellationToken),
                facility, discharge?.SignedAt ?? admission.AdmittedAt, careContext.ReferenceNumber);

            if (discharge != null && Allowed("DischargeSummary"))
            {
                var medicines = await _db.DischargeMedication.AsNoTracking()
                    .Where(m => m.DischargeSummaryId == discharge.DischargeSummaryId)
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => new FhirMedicine(m.MedicineName, m.Dosage, m.Frequency, m.Durations, m.Route, m.Instructions))
                    .ToListAsync(cancellationToken);
                Add("DischargeSummary", AbdmFhir.DischargeSummary(ipdContext, new FhirDischarge(
                    discharge.ChiefComplaint, discharge.HistoryOfPresentIllness, discharge.CourseInHospital, discharge.ProceduresPerformed,
                    discharge.FinalDiagnosis ?? discharge.AdmittingDiagnosis, discharge.FinalDiagnosisIcd10Code, discharge.FinalDiagnosisIcd10Name,
                    discharge.ConditionAtDischarge, discharge.FollowUpInstructions, discharge.DietInstructions, discharge.ActivityRestrictions, medicines)));
            }

            if (Allowed("DiagnosticReport"))
            {
                var orderIds = await _db.PathologyOrder.AsNoTracking()
                    .Where(o => o.HospitalId == hospitalId && o.AdmissionId == careContext.SourceId)
                    .Select(o => o.OrderId)
                    .ToListAsync(cancellationToken);
                await AddLabReportsAsync(ipdContext, hospitalId, orderIds, Add, cancellationToken);
            }
            return new AbdmBuiltRecords { WhenUtc = admission.AdmittedAt, Records = records };
        }

        private async Task AddLabReportsAsync(FhirContext context, Guid hospitalId, List<Guid> orderIds, Action<string, System.Text.Json.Nodes.JsonObject> add, CancellationToken cancellationToken)
        {
            if (orderIds.Count == 0)
                return;
            var reports = await _db.PathologyReport.AsNoTracking()
                .Where(r => r.HospitalId == hospitalId && orderIds.Contains(r.OrderId))
                .Select(r => new { r.ReportId, r.OrderId, r.ReportNo, r.ApprovedAt, r.GeneratedAt, r.CreatedAt, r.PathologistName, r.PathologistRegNo })
                .ToListAsync(cancellationToken);
            foreach (var report in reports)
            {
                var testIds = await _db.PathologyOrderLine.AsNoTracking()
                    .Where(l => l.OrderId == report.OrderId && l.HospitalId == hospitalId)
                    .Select(l => l.TestId)
                    .ToListAsync(cancellationToken);
                var testNames = await _db.PathologyTestMaster.AsNoTracking()
                    .Where(t => testIds.Contains(t.TestId))
                    .Select(t => t.TestName)
                    .ToListAsync(cancellationToken);
                var interpretations = await _db.PathologyResult.AsNoTracking()
                    .Where(r => r.ReportId == report.ReportId && r.HospitalId == hospitalId && r.Interpretation != null)
                    .Select(r => r.Interpretation!)
                    .ToListAsync(cancellationToken);

                var author = string.IsNullOrWhiteSpace(report.PathologistName)
                    ? context.Author
                    : new FhirAuthor(report.ReportId.ToString(), report.PathologistName, report.PathologistRegNo);
                var reportContext = context with { Author = author, WhenUtc = report.ApprovedAt ?? report.GeneratedAt ?? report.CreatedAt };
                add("DiagnosticReport", AbdmFhir.DiagnosticReport(reportContext, report.ReportNo, testNames,
                    interpretations.Count == 0 ? null : string.Join("\n", interpretations)));
            }
        }

        private async Task<FhirAuthor> AuthorAsync(Guid? doctorId, string? knownName, CancellationToken cancellationToken)
        {
            if (doctorId == null || doctorId == Guid.Empty)
                return new FhirAuthor("unknown", knownName, null);
            var doctor = await _db.Doctors.AsNoTracking()
                .Where(d => d.DoctorID == doctorId)
                .Select(d => new { d.UserID, d.LicenseNumber })
                .FirstOrDefaultAsync(cancellationToken);
            var name = knownName;
            if (string.IsNullOrWhiteSpace(name) && doctor != null)
                name = await _db.UserProfiles.AsNoTracking().Where(u => u.UserID == doctor.UserID).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
            return new FhirAuthor(doctorId.Value.ToString(), name, doctor?.LicenseNumber);
        }

        /// <summary>The vitals JSON saved with an appointment: Bp { Sys, Dia }, Pulse, TempC, Spo2,
        /// HeightCm, WeightKg, Bmi, RespiratoryRate. Key case varies, so it is read loosely.</summary>
        public static FhirVitals? ParseVitals(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;
                var bp = Child(root, "bp");
                return new FhirVitals(
                    bp.ValueKind == JsonValueKind.Object ? Number(bp, "sys") : null,
                    bp.ValueKind == JsonValueKind.Object ? Number(bp, "dia") : null,
                    Number(root, "pulse"), Number(root, "tempC"), Number(root, "spo2"), Number(root, "respiratoryRate"),
                    Number(root, "heightCm"), Number(root, "weightKg"), Number(root, "bmi"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static JsonElement Child(JsonElement element, string name)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return default;
        }

        private static decimal? Number(JsonElement element, string name)
        {
            var value = Child(element, name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                return number;
            return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
    }
}
