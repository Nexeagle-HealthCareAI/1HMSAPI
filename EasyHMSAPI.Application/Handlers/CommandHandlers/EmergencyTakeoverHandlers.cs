using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Emergency takeover (Vita vision, Phase 3). See EmergencyRules for ranking and validation, 1HMSDatabase
    // create_tables_emergency_takeover.sql for the tables.

    /// <summary>Reading listed places and turning rows into what each caller sees.</summary>
    internal static class EmergencyPlaces
    {
        public const string AlertTemplate = "emergency_nearby_alert";          // place_name, reason, distance, time
        public const string NumberTemplate = "emergency_patient_number";       // place_name, reason, patient_mobile
        public const string AlertCode = "EMERGENCY_NEARBY";
        public const string AlertRoles = "Admin,AdminDoctor,Doctor,Nurse,Receptionist";

        /// <summary>Places patients may see: switched on, verified, and (for a 1HMS hospital) active and not archived.</summary>
        public static Task<List<HospitalEmergencySetting>> ListedAsync(AppDbContext db, CancellationToken ct)
            => db.HospitalEmergencySettings.AsNoTracking().Include(s => s.Hospital)
                .Where(s => s.IsEnabled && s.VerificationStatus == "VERIFIED"
                    && (s.HospitalId == null || (s.Hospital != null && s.Hospital.IsActive && !s.Hospital.IsArchived)))
                .ToListAsync(ct);

        public static string NameOf(HospitalEmergencySetting s) => s.Hospital?.Name ?? s.Name ?? "Emergency service";

        public static PublicEmergencyPlaceModel ToPublic(HospitalEmergencySetting s, double km) => new()
        {
            SettingId = s.SettingId,
            HospitalId = s.HospitalId,
            Name = NameOf(s),
            Kind = s.Kind,
            Phone = s.EmergencyPhone,
            AlternatePhone = s.AlternatePhone,
            DistanceKm = Math.Round(km, 1),
            Address = s.Address ?? s.Hospital?.Location,
            Landmark = s.Landmark,
            City = s.City ?? s.Hospital?.City,
            Latitude = (double)s.Latitude,
            Longitude = (double)s.Longitude,
            Is24x7 = s.Is24x7,
            OpenTime = s.OpenTime?.ToString(@"hh\:mm"),
            CloseTime = s.CloseTime?.ToString(@"hh\:mm"),
            Capabilities = EmergencyRules.CapabilityList(s.Capabilities),
        };

        public static EmergencyPlaceModel ToModel(HospitalEmergencySetting s) => new()
        {
            SettingId = s.SettingId,
            HospitalId = s.HospitalId,
            Source = s.Source,
            Name = NameOf(s),
            Kind = s.Kind,
            IsEnabled = s.IsEnabled,
            EmergencyPhone = s.EmergencyPhone,
            AlternatePhone = s.AlternatePhone,
            AlertWhatsApp = s.AlertWhatsApp,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Landmark = s.Landmark,
            Address = s.Address,
            City = s.City,
            Is24x7 = s.Is24x7,
            OpenTime = s.OpenTime?.ToString(@"hh\:mm"),
            CloseTime = s.CloseTime?.ToString(@"hh\:mm"),
            Capabilities = EmergencyRules.CapabilityList(s.Capabilities),
            DutyContactName = s.DutyContactName,
            DutyContactPhone = s.DutyContactPhone,
            TermsVersion = s.TermsVersion,
            TermsAcceptedAt = s.TermsAcceptedAt,
            VerificationStatus = s.VerificationStatus,
            VerifiedAt = s.VerifiedAt,
            VerifiedBy = s.VerifiedBy,
            VerificationNote = s.VerificationNote,
            UpdatedAt = s.UpdatedAt,
            Listed = s.IsEnabled && s.VerificationStatus == "VERIFIED",
        };

        public static string Mask(string mobile) => mobile.Length >= 4 ? mobile[..2] + new string('x', mobile.Length - 4) + mobile[^2..] : "xxxx";

        public static bool ValidPoint(double lat, double lng)
            => lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && !(lat == 0 && lng == 0) && !double.IsNaN(lat) && !double.IsNaN(lng);
    }

    /// <summary>GET /public/emergency-places: nearest emergency-ready places, open now, nearest first.</summary>
    public class GetEmergencyPlacesHandler : IRequestHandler<GetEmergencyPlacesRequestModel, EmergencyPlacesResponseModel>
    {
        private readonly AppDbContext _db;
        public GetEmergencyPlacesHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyPlacesResponseModel> Handle(GetEmergencyPlacesRequestModel r, CancellationToken ct)
        {
            if (!EmergencyPlaces.ValidPoint(r.Latitude, r.Longitude))
                return new EmergencyPlacesResponseModel { Success = false, Message = "A location is needed to find the nearest emergency help." };
            var listed = await EmergencyPlaces.ListedAsync(_db, ct);
            var ranked = EmergencyRules.Rank(listed, r.Latitude, r.Longitude, r.Capability, DateTime.UtcNow, r.Take);
            return new EmergencyPlacesResponseModel { Places = ranked.Select(x => EmergencyPlaces.ToPublic(x.Place, x.Km)).ToList() };
        }
    }

    /// <summary>
    /// POST /public/emergency-alerts: alerts the nearest places (top 5) on WhatsApp and, for 1HMS hospitals, in the 1HMS portal:
    /// "Possible emergency (chest pain) detected 3.2 km from you at 14:05. The patient was shown your number." One chat session alerts
    /// once per emergency (30 minutes). The patient's point is never stored, only the distance to each place.
    /// </summary>
    public class RaiseEmergencyAlertHandler : IRequestHandler<RaiseEmergencyAlertRequestModel, RaiseEmergencyAlertResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly ILogger<RaiseEmergencyAlertHandler> _logger;

        public RaiseEmergencyAlertHandler(AppDbContext db, IWhatsAppMessagingService whatsApp, ILogger<RaiseEmergencyAlertHandler> logger)
        {
            _db = db;
            _whatsApp = whatsApp;
            _logger = logger;
        }

        public async Task<RaiseEmergencyAlertResponseModel> Handle(RaiseEmergencyAlertRequestModel r, CancellationToken ct)
        {
            if (!EmergencyPlaces.ValidPoint(r.Latitude, r.Longitude))
                return new RaiseEmergencyAlertResponseModel { Success = false, Message = "A location is needed to alert the nearest hospitals." };

            var now = DateTime.UtcNow;
            var reason = EmergencyRules.CleanText(r.Reason, 200) ?? "possible emergency";
            var session = EmergencyRules.CleanText(r.SessionRef, 100);
            var channel = EmergencyRules.CleanText(r.Channel, 20)?.ToLowerInvariant() ?? "web";
            var listed = await EmergencyPlaces.ListedAsync(_db, ct);
            var ranked = EmergencyRules.Rank(listed, r.Latitude, r.Longitude, r.Capability, now);
            var places = ranked.Select(x => EmergencyPlaces.ToPublic(x.Place, x.Km)).ToList();

            if (session != null)
            {
                var since = now - EmergencyRules.RepeatWindow;
                var earlier = await _db.EmergencyAlerts.AsNoTracking()
                    .Where(a => a.SessionRef == session && a.Kind == "ALERT" && a.CreatedAt >= since)
                    .OrderByDescending(a => a.CreatedAt).Select(a => (Guid?)a.EventId).FirstOrDefaultAsync(ct);
                if (earlier != null)
                    return new RaiseEmergencyAlertResponseModel { Success = true, EventId = earlier, Places = places, Repeated = true };
            }

            var eventId = Guid.NewGuid();
            if (ranked.Count == 0)
                return new RaiseEmergencyAlertResponseModel { Success = true, EventId = null, Places = places, Message = "No emergency-ready place found nearby." };

            var clock = EmergencyRules.IstClock(now);
            var alerted = 0;
            foreach (var (place, km) in ranked)
            {
                var name = EmergencyPlaces.NameOf(place);
                var distance = $"{Math.Round(km, 1):0.#} km";
                var row = new EmergencyAlert
                {
                    EmergencyAlertId = Guid.NewGuid(), EventId = eventId, SettingId = place.SettingId, HospitalId = place.HospitalId,
                    Kind = "ALERT", Reason = reason, DistanceKm = Math.Round((decimal)km, 2), Channel = channel, SessionRef = session, CreatedAt = now,
                };
                if (place.AlertWhatsApp != null)
                {
                    var sent = await _whatsApp.SendNamedTemplateTrackedAsync(place.AlertWhatsApp, EmergencyPlaces.AlertTemplate, new[]
                    {
                        new KeyValuePair<string, string>("place_name", name),
                        new KeyValuePair<string, string>("reason", reason),
                        new KeyValuePair<string, string>("distance", distance),
                        new KeyValuePair<string, string>("time", clock),
                    });
                    row.WhatsAppSent = sent.Accepted;
                    row.WhatsAppMessageId = sent.MessageId;
                }
                if (place.HospitalId != null)
                {
                    var alert = NewInAppAlert(place.HospitalId.Value, eventId, now, "Possible emergency nearby",
                        $"Possible emergency ({reason}) detected {distance} from you at {clock}. The patient was shown your number.");
                    _db.Alert.Add(alert);
                    row.InAppAlertId = alert.AlertId;
                }
                if (row.WhatsAppSent || row.InAppAlertId != null) alerted++;
                _db.EmergencyAlerts.Add(row);
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Emergency alert {EventId}: {Count} places, {Alerted} reached", eventId, ranked.Count, alerted);
            return new RaiseEmergencyAlertResponseModel { Success = true, EventId = eventId, Places = places, Alerted = alerted };
        }

        internal static Alert NewInAppAlert(Guid hospitalId, Guid eventId, DateTime now, string title, string body) => new()
        {
            AlertId = Guid.NewGuid(),
            HospitalId = hospitalId,
            AlertCode = EmergencyPlaces.AlertCode,
            Severity = "CRITICAL",
            Title = title,
            Body = body,
            AudienceRoles = EmergencyPlaces.AlertRoles,
            Status = "ACTIVE",
            RaisedAt = now,
            RaisedBy = "Vita",
            SourceModule = "EmergencyTakeover",
            SourceRefId = eventId.ToString(),
            DispatchInApp = true,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// POST /public/emergency-alerts/{eventId}/share-number: the patient agreed on screen ("Share my number so the hospital can call
    /// me"); the places alerted in this event get the number on WhatsApp and in the 1HMS portal. Once per event, within 2 hours.
    /// </summary>
    public class ShareEmergencyNumberHandler : IRequestHandler<ShareEmergencyNumberRequestModel, EmergencyActionResponseModel>
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppMessagingService _whatsApp;

        public ShareEmergencyNumberHandler(AppDbContext db, IWhatsAppMessagingService whatsApp)
        {
            _db = db;
            _whatsApp = whatsApp;
        }

        public async Task<EmergencyActionResponseModel> Handle(ShareEmergencyNumberRequestModel r, CancellationToken ct)
        {
            if (!r.Consent) return new EmergencyActionResponseModel { Message = "The number is shared only when the patient agrees." };
            var mobile = EmergencyRules.CleanMobile(r.Mobile);
            if (mobile == null) return new EmergencyActionResponseModel { Message = "Enter a 10-digit mobile number." };

            var now = DateTime.UtcNow;
            var alerts = await _db.EmergencyAlerts.Where(a => a.EventId == r.EventId && (a.Kind == "ALERT" || a.Kind == "NUMBER_SHARED")).ToListAsync(ct);
            var sentAlerts = alerts.Where(a => a.Kind == "ALERT").ToList();
            if (sentAlerts.Count == 0) return new EmergencyActionResponseModel { Message = "This emergency was not found." };
            if (now - sentAlerts.Min(a => a.CreatedAt) > EmergencyRules.ShareWindow)
                return new EmergencyActionResponseModel { Message = "This emergency is too old to share a number. Please call the hospital." };
            if (alerts.Any(a => a.Kind == "NUMBER_SHARED"))
                return new EmergencyActionResponseModel { Success = true, Message = "Already shared.", Count = alerts.Count(a => a.Kind == "NUMBER_SHARED") };

            var settingIds = sentAlerts.Select(a => a.SettingId).ToList();
            var places = await _db.HospitalEmergencySettings.AsNoTracking().Include(s => s.Hospital)
                .Where(s => settingIds.Contains(s.SettingId)).ToListAsync(ct);
            var shared = 0;
            foreach (var a in sentAlerts)
            {
                var place = places.FirstOrDefault(p => p.SettingId == a.SettingId);
                if (place == null) continue;
                var row = new EmergencyAlert
                {
                    EmergencyAlertId = Guid.NewGuid(), EventId = a.EventId, SettingId = a.SettingId, HospitalId = a.HospitalId,
                    Kind = "NUMBER_SHARED", Reason = a.Reason, DistanceKm = a.DistanceKm, Channel = a.Channel, SessionRef = a.SessionRef,
                    PatientMobile = mobile, CreatedAt = now,
                };
                if (place.AlertWhatsApp != null)
                {
                    var sent = await _whatsApp.SendNamedTemplateTrackedAsync(place.AlertWhatsApp, EmergencyPlaces.NumberTemplate, new[]
                    {
                        new KeyValuePair<string, string>("place_name", EmergencyPlaces.NameOf(place)),
                        new KeyValuePair<string, string>("reason", a.Reason ?? "possible emergency"),
                        new KeyValuePair<string, string>("patient_mobile", mobile),
                    });
                    row.WhatsAppSent = sent.Accepted;
                    row.WhatsAppMessageId = sent.MessageId;
                }
                if (a.HospitalId != null)
                {
                    var alert = RaiseEmergencyAlertHandler.NewInAppAlert(a.HospitalId.Value, a.EventId, now, "Emergency patient shared their number",
                        $"The patient with a possible emergency ({a.Reason}) {a.DistanceKm:0.#} km away asked to be called: {mobile}.");
                    _db.Alert.Add(alert);
                    row.InAppAlertId = alert.AlertId;
                }
                _db.EmergencyAlerts.Add(row);
                shared++;
            }
            await _db.SaveChangesAsync(ct);
            return new EmergencyActionResponseModel { Success = true, Count = shared };
        }
    }

    /// <summary>POST /public/emergency-alerts/{eventId}/call: the patient tapped a call button (audit). Only for a place shown in
    /// that event; repeated taps within a minute count once.</summary>
    public class LogEmergencyCallHandler : IRequestHandler<LogEmergencyCallRequestModel, EmergencyActionResponseModel>
    {
        private readonly AppDbContext _db;
        public LogEmergencyCallHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyActionResponseModel> Handle(LogEmergencyCallRequestModel r, CancellationToken ct)
        {
            var alert = await _db.EmergencyAlerts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.EventId == r.EventId && a.SettingId == r.SettingId && a.Kind == "ALERT", ct);
            if (alert == null) return new EmergencyActionResponseModel { Message = "Not part of this emergency." };
            var now = DateTime.UtcNow;
            var recent = now.AddMinutes(-1);
            if (await _db.EmergencyAlerts.AnyAsync(a => a.EventId == r.EventId && a.SettingId == r.SettingId && a.Kind == "CALL_TAP" && a.CreatedAt >= recent, ct))
                return new EmergencyActionResponseModel { Success = true };
            _db.EmergencyAlerts.Add(new EmergencyAlert
            {
                EmergencyAlertId = Guid.NewGuid(), EventId = r.EventId, SettingId = r.SettingId, HospitalId = alert.HospitalId, Kind = "CALL_TAP",
                Reason = alert.Reason, DistanceKm = alert.DistanceKm, Channel = alert.Channel, SessionRef = alert.SessionRef, CreatedAt = now,
            });
            await _db.SaveChangesAsync(ct);
            return new EmergencyActionResponseModel { Success = true, Count = 1 };
        }
    }

    /// <summary>1HMS portal, Settings -> Emergency takeover: the hospital's opt-in, or a new one filled from the hospital's own details.</summary>
    public class GetHospitalEmergencySettingsHandler : IRequestHandler<GetHospitalEmergencySettingsRequestModel, HospitalEmergencySettingsResponseModel>
    {
        private readonly AppDbContext _db;
        public GetHospitalEmergencySettingsHandler(AppDbContext db) { _db = db; }

        public async Task<HospitalEmergencySettingsResponseModel> Handle(GetHospitalEmergencySettingsRequestModel r, CancellationToken ct)
        {
            var response = new HospitalEmergencySettingsResponseModel { CurrentTermsVersion = EmergencyRules.TermsVersion, CapabilityOptions = EmergencyRules.Capabilities };
            if (!await CallerGuards.IsHospitalMemberAsync(_db, r.CallerUserId, r.HospitalId, ct))
            {
                response.Message = "You don't have access to this hospital.";
                return response;
            }
            var row = await _db.HospitalEmergencySettings.AsNoTracking().Include(s => s.Hospital).FirstOrDefaultAsync(s => s.HospitalId == r.HospitalId, ct);
            if (row != null)
            {
                response.Success = true;
                response.Settings = EmergencyPlaces.ToModel(row);
                return response;
            }
            var h = await _db.Hospitals.AsNoTracking().FirstOrDefaultAsync(x => x.HospitalID == r.HospitalId, ct);
            if (h == null)
            {
                response.Message = "Hospital not found.";
                return response;
            }
            response.Success = true;
            response.Settings = new EmergencyPlaceModel
            {
                HospitalId = h.HospitalID, Name = h.Name, EmergencyPhone = EmergencyRules.CleanPhone(h.Contact), AlternatePhone = EmergencyRules.CleanPhone(h.AlternateContact),
                Latitude = h.Latitude, Longitude = h.Longitude, Address = h.Location, City = h.City, VerificationStatus = "NEW",
            };
            return response;
        }
    }

    /// <summary>Saves the hospital's opt-in (admins only). Switching on needs the current terms accepted. A change to the numbers, pin or
    /// hours of a verified listing sends it back to the CMS for checking (it stays off for patients until then).</summary>
    public class SaveHospitalEmergencySettingsHandler : IRequestHandler<SaveHospitalEmergencySettingsRequestModel, HospitalEmergencySettingsResponseModel>
    {
        private readonly AppDbContext _db;
        public SaveHospitalEmergencySettingsHandler(AppDbContext db) { _db = db; }

        public async Task<HospitalEmergencySettingsResponseModel> Handle(SaveHospitalEmergencySettingsRequestModel r, CancellationToken ct)
        {
            var response = new HospitalEmergencySettingsResponseModel { CurrentTermsVersion = EmergencyRules.TermsVersion, CapabilityOptions = EmergencyRules.Capabilities };
            if (!await CallerGuards.HasPermissionAtHospitalAsync(_db, r.CallerUserId, r.HospitalId, "admin_panel", ct))
            {
                response.Message = "Only a hospital admin can change the emergency takeover.";
                return response;
            }
            var problem = EmergencyRules.Validate(r);
            if (problem != null)
            {
                response.Message = problem;
                return response;
            }
            if (r.IsEnabled && EmergencyRules.CleanMobile(r.AlertWhatsApp) == null)
            {
                response.Message = "Add a WhatsApp number for alerts before switching the takeover on.";
                return response;
            }
            var now = DateTime.UtcNow;
            var row = await _db.HospitalEmergencySettings.Include(s => s.Hospital).FirstOrDefaultAsync(s => s.HospitalId == r.HospitalId, ct);
            var isNew = row == null;
            if (row == null)
            {
                row = new HospitalEmergencySetting { SettingId = Guid.NewGuid(), HospitalId = r.HospitalId, Source = "HOSPITAL", CreatedAt = now, VerificationStatus = "PENDING" };
                _db.HospitalEmergencySettings.Add(row);
            }
            if (r.AcceptTerms)
            {
                row.TermsVersion = EmergencyRules.TermsVersion;
                row.TermsAcceptedAt = now;
                row.TermsAcceptedByUserId = r.CallerUserId;
            }
            if (r.IsEnabled && row.TermsVersion != EmergencyRules.TermsVersion)
            {
                response.Message = "Accept the emergency takeover terms to switch it on.";
                return response;
            }
            var changed = EmergencyRules.Apply(row, r);
            row.Kind = "HOSPITAL";
            row.IsEnabled = r.IsEnabled;
            if (row.VerificationStatus != "PENDING" && (changed || row.VerificationStatus == "REJECTED"))
            {
                row.VerificationStatus = "PENDING";
                row.VerifiedAt = null;
                row.VerifiedBy = null;
                row.VerificationNote = changed ? "Numbers, map pin or hours changed by the hospital." : row.VerificationNote;
            }
            row.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            row.Hospital ??= await _db.Hospitals.AsNoTracking().FirstOrDefaultAsync(h => h.HospitalID == r.HospitalId, ct);
            response.Success = true;
            response.Settings = EmergencyPlaces.ToModel(row);
            response.SentForVerification = row.VerificationStatus == "PENDING" && (isNew || changed);
            response.Message = row.VerificationStatus == "PENDING"
                ? "Saved. Doctor Dekho will check the details before patients can see your hospital in an emergency."
                : "Saved.";
            return response;
        }
    }

    /// <summary>CMS: every opt-in and CMS-added place, with counts per status.</summary>
    public class ListEmergencyPlacesHandler : IRequestHandler<ListEmergencyPlacesRequestModel, EmergencyPlaceListResponseModel>
    {
        private readonly AppDbContext _db;
        public ListEmergencyPlacesHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyPlaceListResponseModel> Handle(ListEmergencyPlacesRequestModel r, CancellationToken ct)
        {
            var all = await _db.HospitalEmergencySettings.AsNoTracking().Include(s => s.Hospital).ToListAsync(ct);
            var status = (r.Status ?? string.Empty).Trim().ToUpperInvariant();
            return new EmergencyPlaceListResponseModel
            {
                Items = all.Where(s => status.Length == 0 || s.VerificationStatus == status)
                    .OrderBy(s => s.VerificationStatus == "PENDING" ? 0 : 1).ThenByDescending(s => s.UpdatedAt)
                    .Select(EmergencyPlaces.ToModel).ToList(),
                Pending = all.Count(s => s.VerificationStatus == "PENDING"),
                Verified = all.Count(s => s.VerificationStatus == "VERIFIED"),
                Rejected = all.Count(s => s.VerificationStatus == "REJECTED"),
            };
        }
    }

    /// <summary>CMS: a Doctor Dekho admin checked a hospital's details (called the number, checked the pin) and verifies or rejects it.</summary>
    public class VerifyEmergencyPlaceHandler : IRequestHandler<VerifyEmergencyPlaceRequestModel, EmergencyActionResponseModel>
    {
        private readonly AppDbContext _db;
        public VerifyEmergencyPlaceHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyActionResponseModel> Handle(VerifyEmergencyPlaceRequestModel r, CancellationToken ct)
        {
            var decision = (r.Decision ?? string.Empty).Trim().ToUpperInvariant();
            if (decision != "VERIFIED" && decision != "REJECTED") return new EmergencyActionResponseModel { Message = "Decision must be VERIFIED or REJECTED." };
            var note = EmergencyRules.CleanText(r.Note, 500);
            if (decision == "REJECTED" && note == null) return new EmergencyActionResponseModel { Message = "Say why, so the hospital can fix it." };
            var row = await _db.HospitalEmergencySettings.FirstOrDefaultAsync(s => s.SettingId == r.SettingId, ct);
            if (row == null) return new EmergencyActionResponseModel { Message = "Not found." };
            row.VerificationStatus = decision;
            row.VerifiedAt = DateTime.UtcNow;
            row.VerifiedBy = EmergencyRules.CleanText(r.Actor, 200) ?? "CMS";
            row.VerificationNote = note;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new EmergencyActionResponseModel { Success = true, Count = 1 };
        }
    }

    /// <summary>CMS: add or change a place that is not on 1HMS (a district hospital, an ambulance service). Added by an admin, so it is
    /// verified by that admin.</summary>
    public class SaveCmsEmergencyPlaceHandler : IRequestHandler<SaveCmsEmergencyPlaceRequestModel, EmergencyPlaceListResponseModel>
    {
        private readonly AppDbContext _db;
        public SaveCmsEmergencyPlaceHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyPlaceListResponseModel> Handle(SaveCmsEmergencyPlaceRequestModel r, CancellationToken ct)
        {
            var name = EmergencyRules.CleanText(r.Name, 200);
            if (name == null) return new EmergencyPlaceListResponseModel { Success = false, Message = "Enter the name." };
            var problem = EmergencyRules.Validate(r);
            if (problem != null) return new EmergencyPlaceListResponseModel { Success = false, Message = problem };
            var now = DateTime.UtcNow;
            HospitalEmergencySetting? row = null;
            if (r.SettingId != null)
            {
                row = await _db.HospitalEmergencySettings.FirstOrDefaultAsync(s => s.SettingId == r.SettingId, ct);
                if (row == null) return new EmergencyPlaceListResponseModel { Success = false, Message = "Not found." };
                if (row.Source != "CMS") return new EmergencyPlaceListResponseModel { Success = false, Message = "A 1HMS hospital changes its own details from the 1HMS portal." };
            }
            if (row == null)
            {
                row = new HospitalEmergencySetting { SettingId = Guid.NewGuid(), Source = "CMS", CreatedAt = now };
                _db.HospitalEmergencySettings.Add(row);
            }
            EmergencyRules.Apply(row, r);
            row.Name = name;
            row.IsEnabled = r.IsEnabled;
            row.VerificationStatus = "VERIFIED";
            row.VerifiedAt = now;
            row.VerifiedBy = EmergencyRules.CleanText(r.Actor, 200) ?? "CMS";
            row.VerificationNote = "Added by a CMS admin.";
            row.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return new EmergencyPlaceListResponseModel { Items = { EmergencyPlaces.ToModel(row) } };
        }
    }

    /// <summary>CMS "Emergency alerts" log: each emergency with the places alerted, whether WhatsApp went out, call taps, acknowledgements
    /// in 1HMS, and whether the patient shared their number (masked).</summary>
    public class GetEmergencyAlertLogHandler : IRequestHandler<GetEmergencyAlertLogRequestModel, EmergencyAlertLogResponseModel>
    {
        private readonly AppDbContext _db;
        public GetEmergencyAlertLogHandler(AppDbContext db) { _db = db; }

        public async Task<EmergencyAlertLogResponseModel> Handle(GetEmergencyAlertLogRequestModel r, CancellationToken ct)
        {
            var to = r.ToUtc ?? DateTime.UtcNow;
            var from = r.FromUtc ?? to.AddDays(-30);
            var take = Math.Clamp(r.Take, 1, 500);
            var eventIds = await _db.EmergencyAlerts.AsNoTracking()
                .Where(a => a.Kind == "ALERT" && a.CreatedAt >= from && a.CreatedAt <= to)
                .GroupBy(a => a.EventId).Select(g => new { g.Key, At = g.Min(a => a.CreatedAt) })
                .OrderByDescending(x => x.At).Take(take).ToListAsync(ct);
            var ids = eventIds.Select(e => e.Key).ToList();
            var rows = await _db.EmergencyAlerts.AsNoTracking().Where(a => ids.Contains(a.EventId)).ToListAsync(ct);
            var settingIds = rows.Select(a => a.SettingId).Distinct().ToList();
            var places = await _db.HospitalEmergencySettings.AsNoTracking().Include(s => s.Hospital)
                .Where(s => settingIds.Contains(s.SettingId)).ToListAsync(ct);
            var inAppIds = rows.Where(a => a.InAppAlertId != null).Select(a => a.InAppAlertId!.Value).ToList();
            var acks = await _db.Alert.AsNoTracking().Where(a => inAppIds.Contains(a.AlertId) && a.AcknowledgedAt != null)
                .Select(a => new { a.AlertId, a.AcknowledgedBy, a.AcknowledgeNote }).ToListAsync(ct);

            var response = new EmergencyAlertLogResponseModel();
            foreach (var e in eventIds)
            {
                var mine = rows.Where(a => a.EventId == e.Key).ToList();
                var first = mine.Where(a => a.Kind == "ALERT").OrderBy(a => a.CreatedAt).First();
                var shared = mine.FirstOrDefault(a => a.Kind == "NUMBER_SHARED")?.PatientMobile;
                response.Events.Add(new EmergencyAlertLogEvent
                {
                    EventId = e.Key, At = e.At, Reason = first.Reason, Channel = first.Channel,
                    SharedMobile = shared == null ? null : EmergencyPlaces.Mask(shared),
                    Places = mine.Where(a => a.Kind == "ALERT").Select(a =>
                    {
                        var ack = acks.FirstOrDefault(k => k.AlertId == a.InAppAlertId);
                        var place = places.FirstOrDefault(p => p.SettingId == a.SettingId);
                        return new EmergencyAlertLogPlace
                        {
                            SettingId = a.SettingId, Name = place == null ? "(removed)" : EmergencyPlaces.NameOf(place), DistanceKm = a.DistanceKm,
                            WhatsAppSent = a.WhatsAppSent, InApp = a.InAppAlertId != null,
                            CallTaps = mine.Count(c => c.Kind == "CALL_TAP" && c.SettingId == a.SettingId),
                            Acknowledged = ack == null ? null : (string.Join(": ", new[] { ack.AcknowledgedBy, ack.AcknowledgeNote }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } who ? who : "Acknowledged"),
                        };
                    }).OrderBy(p => p.DistanceKm).ToList(),
                });
            }
            return response;
        }
    }
}
