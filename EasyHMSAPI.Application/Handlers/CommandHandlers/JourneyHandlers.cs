using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Vita "takes you there": the patient's trip to the clinic. See Services/Journeys.cs for the rules.

    internal static class JourneyGate
    {
        /// <summary>The appointment, or why the journey can't be touched (null message = fine).</summary>
        public static async Task<(Appointment? Appt, string? Problem, bool NotFound)> LoadAsync(AppDbContext db, Guid apptId, CancellationToken ct)
        {
            var a = await db.Appointments.FirstOrDefaultAsync(x => x.ApptId == apptId, ct);
            if (a == null) return (null, "Appointment not found.", true);
            if (!Journeys.IsPublic(a)) return (a, "Only Doctor Dekho bookings can share a journey.", false);
            if (!VisitMessages.IsOpen(a)) return (a, "This appointment is cancelled or already completed.", false);
            return (a, null, false);
        }

        public static async Task<AppointmentJourney> RowAsync(AppDbContext db, Guid apptId, DateTime nowUtc, CancellationToken ct)
        {
            var j = await db.AppointmentJourneys.FirstOrDefaultAsync(x => x.ApptId == apptId, ct);
            if (j != null) return j;
            j = new AppointmentJourney { ApptId = apptId, Status = Journeys.Planned, CreatedAt = nowUtc, UpdatedAt = nowUtc };
            db.AppointmentJourneys.Add(j);
            return j;
        }

        public static PublicJourneyResponseModel Fail(string message) => new() { Message = message };
    }

    /// <summary>PUT /public/appointments/{id}/journey: travel mode, leave time, the OK to show reception the ETA.</summary>
    public class SavePublicJourneyHandler : IRequestHandler<SavePublicJourneyRequestModel, PublicJourneyResponseModel>
    {
        private readonly AppDbContext _db;
        public SavePublicJourneyHandler(AppDbContext db) { _db = db; }

        public async Task<PublicJourneyResponseModel> Handle(SavePublicJourneyRequestModel r, CancellationToken ct)
        {
            var (a, problem, _) = await JourneyGate.LoadAsync(_db, r.AppointmentId, ct);
            if (problem != null) return JourneyGate.Fail(problem);

            string? mode = null;
            if (r.TravelMode != null && (mode = Journeys.CleanMode(r.TravelMode)) == null)
                return JourneyGate.Fail("Travel mode must be drive, auto_cab, transit or walk.");
            DateTime? leave = null;
            if (!string.IsNullOrWhiteSpace(r.LeaveAt))
            {
                leave = Journeys.ParseLeave(r.LeaveAt, a!);
                if (leave == null) return JourneyGate.Fail("Leave time must look like 08:15 or 2026-10-26T08:15.");
                var bad = Journeys.LeaveProblem(leave.Value, a!);
                if (bad != null) return JourneyGate.Fail(bad);
            }

            var j = await JourneyGate.RowAsync(_db, a!.ApptId, r.NowUtc, ct);
            if (mode != null) j.TravelMode = mode;
            if (leave != null && leave != j.PlannedLeaveAt)
            {
                j.PlannedLeaveAt = leave;
                if (j.Status == Journeys.Planned) j.LeaveReminderSentAt = null;      // a new time gets its own reminder
            }
            if (r.ShareWithHospital != null) j.ShareWithHospital = r.ShareWithHospital.Value;
            if (j.Status == Journeys.Cancelled) j.Status = Journeys.Planned;           // planning again after dropping it
            j.UpdatedAt = r.NowUtc;
            await _db.SaveChangesAsync(ct);
            return new PublicJourneyResponseModel { Success = true, Journey = Journeys.ToPublic(j, r.NowUtc) };
        }
    }

    /// <summary>POST /public/appointments/{id}/journey/progress: on the way (ETA, distance left), late, arrived, cancelled. An ETA past
    /// the appointment time marks the journey late by itself, so reception sees it without the patient having to say so.</summary>
    public class UpdatePublicJourneyHandler : IRequestHandler<UpdatePublicJourneyRequestModel, PublicJourneyResponseModel>
    {
        private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);
        private readonly AppDbContext _db;
        public UpdatePublicJourneyHandler(AppDbContext db) { _db = db; }

        public async Task<PublicJourneyResponseModel> Handle(UpdatePublicJourneyRequestModel r, CancellationToken ct)
        {
            var status = (r.Status ?? "").Trim().ToLowerInvariant();
            if (!Journeys.Progress.Contains(status)) return JourneyGate.Fail("Status must be on_the_way, late, arrived or cancelled.");
            if (r.EtaMinutes is < 0 or > 600) return JourneyGate.Fail("ETA must be between 0 and 600 minutes.");
            if (r.DistanceKm is < 0m or > 2000m) return JourneyGate.Fail("Distance must be between 0 and 2000 km.");
            if (r.LateByMinutes is < 0 or > 600) return JourneyGate.Fail("Late by must be between 0 and 600 minutes.");

            var (a, problem, _) = await JourneyGate.LoadAsync(_db, r.AppointmentId, ct);
            if (problem != null) return JourneyGate.Fail(problem);
            var today = VisitMessages.ToIst(r.NowUtc).Date;
            if (a!.ApptDate.Date < today.AddDays(-1) || a.ApptDate.Date > today.AddDays(1))
                return JourneyGate.Fail("The journey can only be updated around the day of the appointment.");

            var j = await JourneyGate.RowAsync(_db, a.ApptId, r.NowUtc, ct);
            if (j.Status == Journeys.Arrived && status != Journeys.Cancelled)
                return new PublicJourneyResponseModel { Success = true, Journey = Journeys.ToPublic(j, r.NowUtc) };   // already there

            switch (status)
            {
                case Journeys.OnTheWay:
                    j.StartedAt ??= r.NowUtc;
                    if (r.EtaMinutes != null) j.EtaAt = r.NowUtc.AddMinutes(r.EtaMinutes.Value);
                    if (r.DistanceKm != null) j.DistanceKm = r.DistanceKm;
                    MarkLateFromEta(j, a);
                    break;
                case Journeys.Late:
                    j.StartedAt ??= r.NowUtc;
                    j.Status = Journeys.Late;
                    if (r.LateByMinutes != null) j.LateByMinutes = r.LateByMinutes;
                    if (r.EtaMinutes != null) j.EtaAt = r.NowUtc.AddMinutes(r.EtaMinutes.Value);
                    else if (r.LateByMinutes != null && VisitMessages.HasTime(a))
                        j.EtaAt = a.StartAt.AddMinutes(r.LateByMinutes.Value) - VisitMessages.IstOffset;   // StartAt is IST wall-clock
                    if (r.DistanceKm != null) j.DistanceKm = r.DistanceKm;
                    break;
                case Journeys.Arrived:
                    j.Status = Journeys.Arrived;
                    j.ArrivedAt = r.NowUtc;
                    j.EtaAt = null;
                    j.DistanceKm = 0;
                    break;
                default:
                    j.Status = Journeys.Cancelled;
                    j.EtaAt = null;
                    break;
            }
            j.UpdatedAt = r.NowUtc;
            await _db.SaveChangesAsync(ct);
            return new PublicJourneyResponseModel { Success = true, Journey = Journeys.ToPublic(j, r.NowUtc) };
        }

        private static void MarkLateFromEta(AppointmentJourney j, Appointment a)
        {
            if (j.EtaAt == null || !VisitMessages.HasTime(a))
            {
                if (j.Status != Journeys.Late) j.Status = Journeys.OnTheWay;
                return;
            }
            var arriveIst = VisitMessages.ToIst(j.EtaAt.Value);
            if (arriveIst > a.StartAt + Grace)
            {
                j.Status = Journeys.Late;
                j.LateByMinutes = (int)Math.Ceiling((arriveIst - a.StartAt).TotalMinutes);
            }
            else
            {
                j.Status = Journeys.OnTheWay;
                j.LateByMinutes = null;
            }
        }
    }
}
