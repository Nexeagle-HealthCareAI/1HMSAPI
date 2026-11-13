using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Emergency takeover (Vita vision, Phase 3): only verified, switched-on, open places are shown, nearest first; alerts go to the top
    /// five once per chat session; a number is shared only with consent; hospitals opt in through their admin and changes go back for
    /// verification; the CMS verifies and adds places not on 1HMS.
    /// </summary>
    [TestFixture]
    public class EmergencyTakeoverTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private readonly List<(string To, string Template, IReadOnlyList<KeyValuePair<string, string>> Params)> _sent = new();
        private static CancellationToken Ct => CancellationToken.None;

        // Kolkata, Salt Lake: the patient
        private const double Lat = 22.5800, Lng = 88.4100;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _sent.Clear();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendNamedTemplateTrackedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>()))
                .Callback<string, string, IReadOnlyList<KeyValuePair<string, string>>>((to, t, p) => _sent.Add((to, t, p)))
                .ReturnsAsync(new WhatsAppSendResult { Accepted = true, MessageId = "wamid.1" });
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Hospital AddHospital(string name, bool archived = false)
        {
            var h = new Hospital
            {
                HospitalID = Guid.NewGuid(), Name = name, Type = "General", RegistrationNumber = "REG", Contact = "03340000000", Location = "Sector V",
                City = "Kolkata", State = "WB", Country = "India", Pincode = "700091", CreatedByUserID = Guid.NewGuid(), IsArchived = archived,
                Latitude = 22.58m, Longitude = 88.42m,
            };
            _context.Hospitals.Add(h);
            _context.SaveChanges();
            return h;
        }

        /// <summary>A listed place about <paramref name="kmEast"/> km east of the patient.</summary>
        private HospitalEmergencySetting AddPlace(string name, double kmEast, string status = "VERIFIED", bool enabled = true, Hospital? hospital = null,
            string? capabilities = null, bool is24x7 = true, TimeSpan? open = null, TimeSpan? close = null, string? whatsapp = "9830000001")
        {
            var s = new HospitalEmergencySetting
            {
                SettingId = Guid.NewGuid(), HospitalId = hospital?.HospitalID, Hospital = hospital, Source = hospital == null ? "CMS" : "HOSPITAL",
                Name = hospital == null ? name : null, IsEnabled = enabled, EmergencyPhone = "03312345678", AlertWhatsApp = whatsapp,
                Latitude = (decimal)Lat, Longitude = (decimal)(Lng + kmEast / 103.0), Is24x7 = is24x7, OpenTime = open, CloseTime = close,
                Capabilities = capabilities, VerificationStatus = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HospitalEmergencySettings.Add(s);
            _context.SaveChanges();
            return s;
        }

        private RaiseEmergencyAlertHandler Raiser() => new(_context, _whatsApp.Object, NullLogger<RaiseEmergencyAlertHandler>.Instance);

        // ------------------------------------------------------------------ listing
        [Test]
        public async Task Lists_only_verified_switched_on_open_places_nearest_first()
        {
            AddPlace("Far", 12);
            AddPlace("Near", 2);
            AddPlace("Pending", 1, status: "PENDING");
            AddPlace("Switched off", 1, enabled: false);
            AddPlace("Archived hospital", 1, hospital: AddHospital("Old", archived: true));
            var now = DateTime.UtcNow + TimeSpan.FromHours(5.5);
            AddPlace("Closed now", 1, is24x7: false, open: now.TimeOfDay.Add(TimeSpan.FromHours(2)), close: now.TimeOfDay.Add(TimeSpan.FromHours(3)));
            var r = await new GetEmergencyPlacesHandler(_context).Handle(new GetEmergencyPlacesRequestModel { Latitude = Lat, Longitude = Lng }, Ct);
            Assert.That(r.Places.Select(p => p.Name), Is.EqualTo(new[] { "Near", "Far" }));
            Assert.That(r.Places[0].DistanceKm, Is.EqualTo(2).Within(0.2));
        }

        [Test]
        public async Task Beyond_25_km_only_while_fewer_than_three_are_near()
        {
            AddPlace("A", 3);
            AddPlace("B", 40);
            AddPlace("C", 60);
            AddPlace("D", 80);
            var r = await new GetEmergencyPlacesHandler(_context).Handle(new GetEmergencyPlacesRequestModel { Latitude = Lat, Longitude = Lng }, Ct);
            Assert.That(r.Places.Select(p => p.Name), Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public async Task A_capability_match_moves_up_only_against_nearly_as_close_places()
        {
            AddPlace("General", 3);
            AddPlace("Heart centre", 4.5, capabilities: "cardiac,icu");
            AddPlace("Far heart centre", 15, capabilities: "cardiac");
            var r = await new GetEmergencyPlacesHandler(_context).Handle(new GetEmergencyPlacesRequestModel { Latitude = Lat, Longitude = Lng, Capability = "cardiac" }, Ct);
            Assert.That(r.Places.Select(p => p.Name), Is.EqualTo(new[] { "Heart centre", "General", "Far heart centre" }));
        }

        [Test]
        public void Overnight_hours_are_open_across_midnight()
        {
            var s = new HospitalEmergencySetting { Is24x7 = false, OpenTime = new TimeSpan(20, 0, 0), CloseTime = new TimeSpan(8, 0, 0) };
            Assert.That(EmergencyRules.IsOpenAt(s, new DateTime(2026, 10, 25, 18, 0, 0, DateTimeKind.Utc)), Is.True);   // 23:30 IST
            Assert.That(EmergencyRules.IsOpenAt(s, new DateTime(2026, 10, 25, 7, 0, 0, DateTimeKind.Utc)), Is.False);   // 12:30 IST
        }

        [Test]
        public async Task No_location_no_list()
        {
            var r = await new GetEmergencyPlacesHandler(_context).Handle(new GetEmergencyPlacesRequestModel(), Ct);
            Assert.That(r.Success, Is.False);
        }

        // ------------------------------------------------------------------ alerting
        [Test]
        public async Task Alerts_the_top_five_on_whatsapp_and_in_1hms_once_per_session()
        {
            var hospital = AddHospital("Salt Lake Care");
            AddPlace(null!, 1, hospital: hospital);
            for (var i = 2; i <= 7; i++) AddPlace($"P{i}", i);
            var request = new RaiseEmergencyAlertRequestModel { Latitude = Lat, Longitude = Lng, Reason = "chest pain", Channel = "web", SessionRef = "sess_abc" };
            var r = await Raiser().Handle(request, Ct);

            Assert.That(r.Success && r.EventId != null, Is.True);
            Assert.That(r.Places, Has.Count.EqualTo(5));
            Assert.That(r.Alerted, Is.EqualTo(5));
            Assert.That(_sent, Has.Count.EqualTo(5));
            Assert.That(_sent[0].Template, Is.EqualTo("emergency_nearby_alert"));
            Assert.That(_sent[0].Params.First(p => p.Key == "reason").Value, Is.EqualTo("chest pain"));
            Assert.That(_sent[0].Params.First(p => p.Key == "distance").Value, Is.EqualTo("1 km"));

            var inApp = _context.Alert.Single();
            Assert.That(inApp.HospitalId, Is.EqualTo(hospital.HospitalID));
            Assert.That(inApp.Severity, Is.EqualTo("CRITICAL"));
            Assert.That(inApp.Body, Does.StartWith("Possible emergency (chest pain) detected 1 km from you at ").And.EndWith("The patient was shown your number."));

            var rows = _context.EmergencyAlerts.ToList();
            Assert.That(rows, Has.Count.EqualTo(5));
            Assert.That(rows.All(a => a.Kind == "ALERT" && a.PatientMobile == null && a.WhatsAppSent), Is.True);

            var again = await Raiser().Handle(request, Ct);
            Assert.That(again.Repeated, Is.True);
            Assert.That(again.EventId, Is.EqualTo(r.EventId));
            Assert.That(_sent, Has.Count.EqualTo(5));                     // nothing sent twice
        }

        [Test]
        public async Task Nothing_nearby_alerts_nobody()
        {
            var r = await Raiser().Handle(new RaiseEmergencyAlertRequestModel { Latitude = Lat, Longitude = Lng, Reason = "bleeding" }, Ct);
            Assert.That(r.Success, Is.True);
            Assert.That(r.EventId, Is.Null);
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public async Task The_number_is_shared_only_with_consent_and_once()
        {
            AddPlace("Near", 1);
            AddPlace("Next", 3);
            var raised = await Raiser().Handle(new RaiseEmergencyAlertRequestModel { Latitude = Lat, Longitude = Lng, Reason = "chest pain" }, Ct);
            var share = new ShareEmergencyNumberHandler(_context, _whatsApp.Object);

            var noConsent = await share.Handle(new ShareEmergencyNumberRequestModel { EventId = raised.EventId!.Value, Mobile = "9876543210", Consent = false }, Ct);
            Assert.That(noConsent.Success, Is.False);
            var bad = await share.Handle(new ShareEmergencyNumberRequestModel { EventId = raised.EventId!.Value, Mobile = "12345", Consent = true }, Ct);
            Assert.That(bad.Success, Is.False);

            var ok = await share.Handle(new ShareEmergencyNumberRequestModel { EventId = raised.EventId!.Value, Mobile = "+91 98765 43210", Consent = true }, Ct);
            Assert.That(ok.Success && ok.Count == 2, Is.True);
            var numberMessages = _sent.Where(s => s.Template == "emergency_patient_number").ToList();
            Assert.That(numberMessages, Has.Count.EqualTo(2));
            Assert.That(numberMessages[0].Params.First(p => p.Key == "patient_mobile").Value, Is.EqualTo("9876543210"));

            var twice = await share.Handle(new ShareEmergencyNumberRequestModel { EventId = raised.EventId!.Value, Mobile = "9876543210", Consent = true }, Ct);
            Assert.That(twice.Success, Is.True);
            Assert.That(_sent.Count(s => s.Template == "emergency_patient_number"), Is.EqualTo(2));
            Assert.That(_context.EmergencyAlerts.Count(a => a.PatientMobile != null), Is.EqualTo(2));

            var unknown = await share.Handle(new ShareEmergencyNumberRequestModel { EventId = Guid.NewGuid(), Mobile = "9876543210", Consent = true }, Ct);
            Assert.That(unknown.Success, Is.False);
        }

        [Test]
        public async Task Call_taps_are_logged_for_places_in_the_event_only()
        {
            var near = AddPlace("Near", 1);
            var other = AddPlace("Not shown", 200);
            AddPlace("A", 2);
            AddPlace("B", 3);
            var raised = await Raiser().Handle(new RaiseEmergencyAlertRequestModel { Latitude = Lat, Longitude = Lng, Reason = "fits" }, Ct);
            var log = new LogEmergencyCallHandler(_context);
            Assert.That((await log.Handle(new LogEmergencyCallRequestModel { EventId = raised.EventId!.Value, SettingId = near.SettingId }, Ct)).Success, Is.True);
            Assert.That((await log.Handle(new LogEmergencyCallRequestModel { EventId = raised.EventId!.Value, SettingId = near.SettingId }, Ct)).Success, Is.True);
            Assert.That((await log.Handle(new LogEmergencyCallRequestModel { EventId = raised.EventId!.Value, SettingId = other.SettingId }, Ct)).Success, Is.False);
            Assert.That(_context.EmergencyAlerts.Count(a => a.Kind == "CALL_TAP"), Is.EqualTo(1));     // a second tap within a minute counts once
        }

        // ------------------------------------------------------------------ hospital opt-in
        private SaveHospitalEmergencySettingsRequestModel Form(Guid hospitalId, Guid caller, bool enabled = true, bool terms = true) => new()
        {
            HospitalId = hospitalId, CallerUserId = caller, IsEnabled = enabled, AcceptTerms = terms, EmergencyPhone = "033 4000 1111",
            AlertWhatsApp = "98300 00001", Latitude = 22.58m, Longitude = 88.42m, Is24x7 = true, Capabilities = new List<string> { "Cardiac", "ICU", "x-ray" },
            DutyContactName = "Dr Sen", Landmark = "Near City Centre",
        };

        [Test]
        public async Task A_hospital_admin_opts_in_and_waits_for_verification()
        {
            var h = AddHospital("Salt Lake Care");
            var admin = HrAuthSeed.SeedMember(_context, h.HospitalID, "admin_panel");
            var member = HrAuthSeed.SeedMember(_context, h.HospitalID);
            var save = new SaveHospitalEmergencySettingsHandler(_context);

            var denied = await save.Handle(Form(h.HospitalID, member), Ct);
            Assert.That(denied.Success, Is.False);

            var noTerms = await save.Handle(Form(h.HospitalID, admin, terms: false), Ct);
            Assert.That(noTerms.Message, Does.Contain("terms"));

            var r = await save.Handle(Form(h.HospitalID, admin), Ct);
            Assert.That(r.Success, Is.True);
            Assert.That(r.SentForVerification, Is.True);
            Assert.That(r.Settings!.VerificationStatus, Is.EqualTo("PENDING"));
            Assert.That(r.Settings.Listed, Is.False);
            Assert.That(r.Settings.Capabilities, Is.EqualTo(new[] { "cardiac", "icu" }));
            Assert.That(r.Settings.EmergencyPhone, Is.EqualTo("03340001111"));
            Assert.That(r.Settings.AlertWhatsApp, Is.EqualTo("9830000001"));
            Assert.That(r.Settings.TermsVersion, Is.EqualTo(EmergencyRules.TermsVersion));

            var read = await new GetHospitalEmergencySettingsHandler(_context).Handle(new GetHospitalEmergencySettingsRequestModel { HospitalId = h.HospitalID, CallerUserId = member }, Ct);
            Assert.That(read.Success && read.Settings!.Name == "Salt Lake Care", Is.True);
        }

        [Test]
        public async Task Changing_numbers_of_a_verified_listing_sends_it_back_for_checking()
        {
            var h = AddHospital("Salt Lake Care");
            var admin = HrAuthSeed.SeedMember(_context, h.HospitalID, "admin_panel");
            var save = new SaveHospitalEmergencySettingsHandler(_context);
            var first = await save.Handle(Form(h.HospitalID, admin), Ct);
            await new VerifyEmergencyPlaceHandler(_context).Handle(new VerifyEmergencyPlaceRequestModel { SettingId = first.Settings!.SettingId!.Value, Decision = "verified", Actor = "admin@cms" }, Ct);

            var same = Form(h.HospitalID, admin, terms: false);
            same.DutyContactName = "Dr Roy";                                       // not a patient-facing number: stays verified
            Assert.That((await save.Handle(same, Ct)).Settings!.VerificationStatus, Is.EqualTo("VERIFIED"));

            var changed = Form(h.HospitalID, admin, terms: false);
            changed.EmergencyPhone = "03399998888";
            var r = await save.Handle(changed, Ct);
            Assert.That(r.Settings!.VerificationStatus, Is.EqualTo("PENDING"));
            Assert.That(r.Settings.Listed, Is.False);
        }

        [Test]
        public async Task Switching_on_needs_a_whatsapp_number_and_a_map_pin()
        {
            var h = AddHospital("Salt Lake Care");
            var admin = HrAuthSeed.SeedMember(_context, h.HospitalID, "admin_panel");
            var save = new SaveHospitalEmergencySettingsHandler(_context);
            var noWa = Form(h.HospitalID, admin);
            noWa.AlertWhatsApp = null;
            Assert.That((await save.Handle(noWa, Ct)).Message, Does.Contain("WhatsApp"));
            var noPin = Form(h.HospitalID, admin);
            noPin.Latitude = null;
            Assert.That((await save.Handle(noPin, Ct)).Message, Does.Contain("map pin"));
        }

        // ------------------------------------------------------------------ CMS
        [Test]
        public async Task The_cms_adds_a_place_not_on_1hms_and_it_is_listed_at_once()
        {
            var r = await new SaveCmsEmergencyPlaceHandler(_context).Handle(new SaveCmsEmergencyPlaceRequestModel
            {
                Name = "District Hospital Barasat", Kind = "HOSPITAL", EmergencyPhone = "108", Latitude = (decimal)Lat, Longitude = (decimal)(Lng + 0.05), Actor = "ops@cms",
            }, Ct);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Items[0].VerificationStatus, Is.EqualTo("VERIFIED"));
            var listed = await new GetEmergencyPlacesHandler(_context).Handle(new GetEmergencyPlacesRequestModel { Latitude = Lat, Longitude = Lng }, Ct);
            Assert.That(listed.Places.Single().Phone, Is.EqualTo("108"));
        }

        [Test]
        public async Task Rejecting_needs_a_reason_and_the_log_masks_shared_numbers()
        {
            var p = AddPlace("Near", 1, status: "PENDING");
            var verify = new VerifyEmergencyPlaceHandler(_context);
            Assert.That((await verify.Handle(new VerifyEmergencyPlaceRequestModel { SettingId = p.SettingId, Decision = "REJECTED" }, Ct)).Success, Is.False);
            Assert.That((await verify.Handle(new VerifyEmergencyPlaceRequestModel { SettingId = p.SettingId, Decision = "VERIFIED" }, Ct)).Success, Is.True);

            var raised = await Raiser().Handle(new RaiseEmergencyAlertRequestModel { Latitude = Lat, Longitude = Lng, Reason = "chest pain" }, Ct);
            await new ShareEmergencyNumberHandler(_context, _whatsApp.Object).Handle(new ShareEmergencyNumberRequestModel { EventId = raised.EventId!.Value, Mobile = "9876543210", Consent = true }, Ct);
            await new LogEmergencyCallHandler(_context).Handle(new LogEmergencyCallRequestModel { EventId = raised.EventId!.Value, SettingId = p.SettingId }, Ct);
            var log = await new GetEmergencyAlertLogHandler(_context).Handle(new GetEmergencyAlertLogRequestModel(), Ct);
            var e = log.Events.Single();
            Assert.That(e.Reason, Is.EqualTo("chest pain"));
            Assert.That(e.SharedMobile, Is.EqualTo("98xxxxxx10"));
            Assert.That(e.Places.Single().CallTaps, Is.EqualTo(1));
            Assert.That(e.Places.Single().WhatsAppSent, Is.True);
        }
    }
}
