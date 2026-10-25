using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Vita vision, Phase 4 "After booking": the hospital's patient visit guide, the patient's visit page and the reminders.

    internal static class VisitGuides
    {
        public static string? Clean(string? value, int max)
        {
            var flat = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (flat.Length == 0) return null;
            return flat.Length <= max ? flat : flat[..max];
        }

        public static VisitGuideModel ToModel(HospitalVisitGuide? g) => g == null ? new VisitGuideModel() : new VisitGuideModel
        {
            Landmark = g.Landmark, EntranceNote = g.EntranceNote, OpdLocation = g.OpdLocation, RegistrationDesk = g.RegistrationDesk,
            ParkingNote = g.ParkingNote, PaymentNote = g.PaymentNote, ExtraNote = g.ExtraNote, UpdatedAt = g.UpdatedAt,
        };
    }

    /// <summary>Settings -> Hospital Info -> Patient visit guide (any member reads).</summary>
    public class GetHospitalVisitGuideHandler : IRequestHandler<GetHospitalVisitGuideRequestModel, HospitalVisitGuideResponseModel>
    {
        private readonly AppDbContext _db;
        public GetHospitalVisitGuideHandler(AppDbContext db) { _db = db; }

        public async Task<HospitalVisitGuideResponseModel> Handle(GetHospitalVisitGuideRequestModel r, CancellationToken ct)
        {
            if (!await CallerGuards.IsHospitalMemberAsync(_db, r.CallerUserId, r.HospitalId, ct))
                return new HospitalVisitGuideResponseModel { Message = "You don't have access to this hospital." };
            var guide = await _db.HospitalVisitGuides.AsNoTracking().FirstOrDefaultAsync(g => g.HospitalId == r.HospitalId, ct);
            var pin = await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == r.HospitalId)
                .Select(h => h.Latitude != null && h.Longitude != null).FirstOrDefaultAsync(ct);
            return new HospitalVisitGuideResponseModel { Success = true, Guide = VisitGuides.ToModel(guide), HasMapPin = pin };
        }
    }

    /// <summary>Saves the guide (hospital admins only). Every field is optional; an empty one is left out of what patients read.</summary>
    public class SaveHospitalVisitGuideHandler : IRequestHandler<SaveHospitalVisitGuideRequestModel, HospitalVisitGuideResponseModel>
    {
        private readonly AppDbContext _db;
        public SaveHospitalVisitGuideHandler(AppDbContext db) { _db = db; }

        public async Task<HospitalVisitGuideResponseModel> Handle(SaveHospitalVisitGuideRequestModel r, CancellationToken ct)
        {
            if (!await CallerGuards.HasPermissionAtHospitalAsync(_db, r.CallerUserId, r.HospitalId, "admin_panel", ct))
                return new HospitalVisitGuideResponseModel { Message = "Only a hospital admin can change the patient visit guide." };
            var row = await _db.HospitalVisitGuides.FirstOrDefaultAsync(g => g.HospitalId == r.HospitalId, ct);
            if (row == null)
            {
                row = new HospitalVisitGuide { HospitalId = r.HospitalId };
                _db.HospitalVisitGuides.Add(row);
            }
            row.Landmark = VisitGuides.Clean(r.Landmark, 200);
            row.EntranceNote = VisitGuides.Clean(r.EntranceNote, 300);
            row.OpdLocation = VisitGuides.Clean(r.OpdLocation, 300);
            row.RegistrationDesk = VisitGuides.Clean(r.RegistrationDesk, 300);
            row.ParkingNote = VisitGuides.Clean(r.ParkingNote, 300);
            row.PaymentNote = VisitGuides.Clean(r.PaymentNote, 300);
            row.ExtraNote = VisitGuides.Clean(r.ExtraNote, 500);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = r.CallerUserId;
            await _db.SaveChangesAsync(ct);
            var pin = await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == r.HospitalId)
                .Select(h => h.Latitude != null && h.Longitude != null).FirstOrDefaultAsync(ct);
            return new HospitalVisitGuideResponseModel { Success = true, Message = "Saved.", Guide = VisitGuides.ToModel(row), HasMapPin = pin };
        }
    }

    /// <summary>
    /// The patient's visit page (Doctor Dekho /appointments/visit/{id}, the reminders' "Visit details" button, and Vita's "I've reached"):
    /// where and when, the hospital's visit guide, whether they can check in from the phone now, their token and the expected wait, and
    /// the follow-up date the doctor set. Never the patient's name, mobile or reason: the appointment id is the only gate.
    /// </summary>
    public class GetPublicVisitHandler : IRequestHandler<GetPublicVisitRequestModel, PublicVisitResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly IMediator _mediator;

        public GetPublicVisitHandler(AppDbContext db, IMediator mediator)
        {
            _db = db;
            _mediator = mediator;
        }

        public async Task<PublicVisitResponseModel> Handle(GetPublicVisitRequestModel r, CancellationToken ct)
        {
            var a = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.ApptId == r.AppointmentId, ct);
            if (a == null) return new PublicVisitResponseModel { Message = "Appointment not found." };
            var c = await VisitMessages.LoadAsync(_db, a, ct);
            var hospital = await _db.Hospitals.AsNoTracking().Where(h => h.HospitalID == a.HospitalId)
                .Select(h => new { h.Location, h.Contact }).FirstOrDefaultAsync(ct);
            var todayIst = VisitMessages.ToIst(DateTime.UtcNow).Date;
            var cancelled = a.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled;
            var completed = a.CurrentStatusCode == AppConstants.AppointmentStatus_Completed;
            var hasToken = await _db.AppointmentTokens.AsNoTracking().AnyAsync(t => t.ApptId == a.ApptId, ct);

            PublicVisitToken? token = null;
            if (hasToken)
            {
                var s = await _mediator.Send(new GetQueueTokenStatusRequestModel { AppointmentId = a.ApptId }, ct);
                if (s.Success)
                    token = new PublicVisitToken
                    {
                        TokenNo = s.TokenNo, Status = s.Status, CurrentServingTokenNo = s.CurrentServingTokenNo,
                        PositionInQueue = s.PositionInQueue, EstimatedWaitMinutes = s.EstimatedWaitMinutes,
                    };
            }

            var visit = new PublicVisit
            {
                AppointmentId = a.ApptId,
                Reference = VisitMessages.Reference(a.ApptId),
                StatusCode = a.CurrentStatusCode ?? "",
                Status = PublicAppointmentStatusLabels.ToPatientLabel(a.CurrentStatusCode),
                Confirmed = !cancelled && a.CurrentStatusCode != AppConstants.AppointmentStatus_PreAppointment,
                Cancelled = cancelled,
                Completed = completed,
                DoctorId = a.DoctorId,
                DoctorName = c.DoctorName,
                HospitalId = a.HospitalId,
                HospitalName = c.HospitalName,
                Address = hospital?.Location,
                City = c.City,
                Contact = hospital?.Contact,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                MapUrl = "https://www.google.com/maps/dir/?api=1&destination=" + VisitMessages.Destination(c),
                Date = a.ApptDate.ToString("yyyy-MM-dd"),
                Time = VisitMessages.HasTime(a) ? a.StartAt.ToString("HH:mm") : null,
                IsToday = a.ApptDate.Date == todayIst,
                Fee = c.Fee,
                Guide = VisitGuides.ToModel(c.Guide),
                CanCheckIn = a.ApptDate.Date == todayIst && !cancelled && !completed && !hasToken && c.Latitude != null && c.Longitude != null,
                Token = token,
                FollowUpDate = c.FollowUpDate?.ToString("yyyy-MM-dd"),
                Language = a.PatientLanguage,
            };
            return new PublicVisitResponseModel { Success = true, Visit = visit };
        }
    }

    /// <summary>
    /// One run of the visit messages job: for Doctor Dekho bookings of yesterday and today, sends the morning reminder, the reminder about
    /// two hours before, and the after-visit message, each once (see VisitMessages.Due). A message whose moment has passed is marked as
    /// handled without sending. Every message is marked once tried, even if WhatsApp refused it, so a missing template cannot make the job
    /// send the same message every few minutes.
    /// </summary>
    public class RunVisitMessagesHandler : IRequestHandler<RunVisitMessagesRequestModel, RunVisitMessagesResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly ILogger<RunVisitMessagesHandler> _logger;

        public RunVisitMessagesHandler(AppDbContext db, IWhatsAppMessagingService whatsApp, ILogger<RunVisitMessagesHandler> logger)
        {
            _db = db;
            _whatsApp = whatsApp;
            _logger = logger;
        }

        public async Task<RunVisitMessagesResponseModel> Handle(RunVisitMessagesRequestModel r, CancellationToken ct)
        {
            var result = new RunVisitMessagesResponseModel();
            var today = VisitMessages.ToIst(r.NowUtc).Date;
            var from = today.AddDays(-2);
            var candidates = await _db.Appointments
                .Where(a => a.BookingSource == AppConstants.BookingSource_NexeaglePublic && a.ApptDate >= from && a.ApptDate <= today
                    && (a.MorningReminderSentAt == null || a.BeforeVisitReminderSentAt == null || a.AfterVisitSentAt == null))
                .OrderBy(a => a.StartAt).Take(500).ToListAsync(ct);

            foreach (var a in candidates)
            {
                var due = VisitMessages.Due(a, r.NowUtc);
                if (due == null) continue;
                if (due.StartsWith("skip:"))
                {
                    VisitMessages.MarkHandled(a, due[5..], r.NowUtc);
                    result.Skipped++;
                    continue;
                }
                var context = await VisitMessages.LoadAsync(_db, a, ct);
                var sent = await VisitMessages.SendAsync(_whatsApp, due, a, context);
                VisitMessages.MarkHandled(a, due, r.NowUtc);
                if (sent.Accepted) result.Sent++;
                else result.NotSent++;
            }
            await _db.SaveChangesAsync(ct);
            if (result.Sent + result.NotSent + result.Skipped > 0)
                _logger.LogInformation("Visit messages: {Sent} sent, {NotSent} not sent, {Skipped} skipped", result.Sent, result.NotSent, result.Skipped);
            return result;
        }
    }
}
