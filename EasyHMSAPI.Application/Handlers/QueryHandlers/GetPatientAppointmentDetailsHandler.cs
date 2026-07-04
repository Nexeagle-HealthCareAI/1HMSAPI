using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientAppointmentDetailsHandler : IRequestHandler<GetPatientAppointmentDetailsRequestModel, GetPatientAppointmentDetailsResponseModel>
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public GetPatientAppointmentDetailsHandler(AppDbContext context)
        {
            _context = context;
        }

        private sealed class PaymentProjection
        {
            public Guid EncounterId { get; set; }
            public decimal Amount { get; set; }
            public string? ReceiptNo { get; set; }
            public DateTime PaidAt { get; set; }
        }

        public async Task<GetPatientAppointmentDetailsResponseModel> Handle(GetPatientAppointmentDetailsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GetPatientAppointmentDetailsResponseModel();

            var query = _context.Appointments.AsQueryable();

            query = query.Where(a => a.HospitalId == request.HospitalId);

            if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(a => a.CurrentStatusCode == request.Status);
            }

            if (request.StartDate.HasValue)
            {
                query = query.Where(a => a.ApptDate >= request.StartDate.Value.Date);
            }
            if (request.EndDate.HasValue)
            {
                query = query.Where(a => a.ApptDate <= request.EndDate.Value.Date);
            }

            if (request.DoctorId.HasValue && request.DoctorId != Guid.Empty)
            {
                query = query.Where(a => a.DoctorId == request.DoctorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PatientId))
            {
                query = query.Where(a => a.PatientId == request.PatientId);
            }

            var appts = await query
                .OrderBy(a => a.ApptDate)
                .ToListAsync(cancellationToken);

            var patientIds = appts.Select(a => a.PatientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId!))
                .ToDictionaryAsync(p => p.PatientId!, p => p, cancellationToken);

            var apptIds = appts.Select(a => a.ApptId).ToList();
            var tokens = await _context.AppointmentTokens
                .Where(t => apptIds.Contains(t.ApptId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var doctorNames = await (from a in _context.Appointments
                                    join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                    join u in _context.UserProfiles on d.UserID equals u.UserID
                                    join dp in _context.Departments on d.PrimaryDepartmentID equals dp.DepartmentID into deptJoin
                                    from dept in deptJoin.DefaultIfEmpty()
                                    where appts.Select(x => x.ApptId).Contains(a.ApptId)
                                    select new { a.ApptId, DoctorName = u.FullName, DepartmentId = d.PrimaryDepartmentID, DepartmentName = dept.Name }).ToListAsync(cancellationToken);

            // Referrer ("Referred By") name + type for appointments that carry one — the edit form
            // needs both (plus the id, already on the appointment row) to pre-select the exact
            // referrer instead of re-searching by name.
            var referrerIds = appts.Where(a => a.ReferredByReferrerId.HasValue).Select(a => a.ReferredByReferrerId!.Value).Distinct().ToList();
            var referrerRows = referrerIds.Count == 0
                ? new List<Referrer>()
                : await _context.Referrers
                    .Where(r => referrerIds.Contains(r.ReferrerId))
                    .ToListAsync(cancellationToken);
            var referrerInfoById = referrerRows.ToDictionary(r => r.ReferrerId, r => (Name: r.ReferrerName, Type: r.ReferrerType));

            // ── OPD consult billing status per appointment (mirrors GetConsultTimelineHandler) ──
            var opdEncounters = await _context.Encounter.AsNoTracking()
                .Where(e => e.HospitalId == request.HospitalId
                    && e.SourceType == "Appointments"
                    && e.SourceId != null && apptIds.Contains(e.SourceId.Value)
                    && e.EncounterTypeCode == BillingConstants.EncounterType.Opd)
                .Select(e => new { ApptId = e.SourceId!.Value, e.EncounterId })
                .ToListAsync(cancellationToken);

            var apptToEncounter = opdEncounters
                .GroupBy(e => e.ApptId)
                .ToDictionary(g => g.Key, g => g.First().EncounterId);
            var encIds = opdEncounters.Select(e => e.EncounterId).Distinct().ToList();

            var consultByEncounter = encIds.Count == 0
                ? new Dictionary<Guid, decimal>()
                : await _context.BillingChargeEvent.AsNoTracking()
                    .Where(c => encIds.Contains(c.EncounterId) && c.CategoryCode == "CONSULT")
                    .GroupBy(c => c.EncounterId)
                    .Select(g => new { EncounterId = g.Key, Amount = g.Sum(x => x.NetAmount) })
                    .ToDictionaryAsync(x => x.EncounterId, x => x.Amount, cancellationToken);

            var consultPayments = encIds.Count == 0
                ? new List<PaymentProjection>()
                : await _context.BillingPayment.AsNoTracking()
                    .Where(p => encIds.Contains(p.EncounterId) && p.PaymentType == "PAYMENT")
                    .Select(p => new PaymentProjection { EncounterId = p.EncounterId, Amount = p.Amount, ReceiptNo = p.ReceiptNo, PaidAt = p.PaidAt })
                    .ToListAsync(cancellationToken);

            var paidByEncounter = consultPayments
                .GroupBy(p => p.EncounterId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var receiptByEncounter = consultPayments
                .GroupBy(p => p.EncounterId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PaidAt).Select(x => x.ReceiptNo).FirstOrDefault());

            foreach (var a in appts)
            {
                PatientRegistration? p = null;
                if (!string.IsNullOrEmpty(a.PatientId))
                {
                    patients.TryGetValue(a.PatientId, out p);
                }
                var token = tokens.FirstOrDefault(t => t.ApptId == a.ApptId);
                var doctorInfo = doctorNames.FirstOrDefault(x => x.ApptId == a.ApptId);
                string? doctorName = doctorInfo?.DoctorName;
                Guid? departmentId = doctorInfo?.DepartmentId;
                string? departmentName = doctorInfo?.DepartmentName;

                // OPD consult billing status for this appointment.
                Guid? encId = apptToEncounter.TryGetValue(a.ApptId, out var ce) ? ce : (Guid?)null;
                bool consultCharged = false, consultPaid = false;
                decimal consultAmount = 0m;
                string? consultReceiptNo = null;
                if (encId.HasValue && consultByEncounter.TryGetValue(encId.Value, out var chAmt))
                {
                    consultCharged = true;
                    consultAmount = chAmt;
                    var paid = paidByEncounter.TryGetValue(encId.Value, out var pp) ? pp : 0m;
                    consultPaid = chAmt > 0 && paid >= chAmt;
                    if (consultPaid) receiptByEncounter.TryGetValue(encId.Value, out consultReceiptNo);
                }

                response.Items.Add(new AppointmentDetail
                {
                    AppointmentId = a.ApptId,
                    PatientId = a.PatientId,
                    PatientFullName = p?.FullName,
                    PatientMobile = p?.Mobile,
                    PatientSex = p?.Sex,
                    PatientAge = p?.Age,
                    PatientAgeUnit = p?.AgeUnit,
                    DoctorId = a.DoctorId,
                    DoctorName = doctorName,
                    DepartmentId = departmentId ?? Guid.Empty,
                    DepartmentName = departmentName,
                    AppointmentDate = a.ApptDate,
                    StartAt = a.StartAt,
                    EndAt = a.EndAt,
                    FinalStatusCode = a.CurrentStatusCode,
                    Reason = a.Reason,
                    InsuranceId = a.InsuranceId,
                    PaymentMode = a.PaymentMode,
                    LastStatusAt = a.LastStatusCodeAt,
                    CreatedAt = a.CreatedAt,
                    AppointmentType = a.AppointmentType,
                    ReferrerId = a.ReferredByReferrerId,
                    ReferrerName = a.ReferredByReferrerId.HasValue && referrerInfoById.TryGetValue(a.ReferredByReferrerId.Value, out var rInfo) ? rInfo.Name : null,
                    ReferrerType = a.ReferredByReferrerId.HasValue && referrerInfoById.TryGetValue(a.ReferredByReferrerId.Value, out var rInfo2) ? rInfo2.Type : null,
                    ReferrerRelation = a.ReferrerRelation,
                    GuardianName = p?.GuardianName,
                    GuardianRelation = p?.GuardianRelation,
                    EncounterId = encId,
                    ConsultCharged = consultCharged,
                    ConsultPaid = consultPaid,
                    ConsultAmount = consultAmount,
                    ConsultReceiptNo = consultReceiptNo,
                    Token = token == null ? null : new TokenDetail
                    {
                        TokenId = token.TokenId,
                        TokenNumber = token.TokenNo,
                        CreatedAt = token.CreatedAt
                    },
                    StatusJsonHistory = string.IsNullOrWhiteSpace(a.StatusHistoryJson) ? null :
                        JsonSerializer.Deserialize<List<StatusHistoryModel>>(a.StatusHistoryJson, JsonOptions)

                });
            }

            return response;
        }
    }
}
