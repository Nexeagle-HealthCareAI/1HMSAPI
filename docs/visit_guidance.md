# After booking: confirmation, reminders, visit page (Vita, vision Phase 4)

"The agent stays with the patient until they are inside the clinic." For Doctor Dekho bookings only (BookingSource NEXEAGLE_PUBLIC).

| Message | When | Template |
|---|---|---|
| Confirmation | right after booking | `visit_confirmation_{en,hi,hinglish}` (falls back to `appointment_confirmation` until approved) |
| Morning reminder | visit day, from 07:30 IST; not if booked that morning | `visit_reminder_morning_*` |
| Soon | about 2 h before the time (only when the booking has a time); not if booked later than that | `visit_reminder_soon_*` |
| After the visit | 2 h after the visit is marked COMPLETED, between 08:00 and 21:30 IST | `visit_after_*` |
| Time to leave | at the leave time the patient gave Vita (`AppointmentJourneys.PlannedLeaveAt`), until 30 min after it; not when already on the way | `visit_leave_now_*` |

Each goes once (`Appointments.ConfirmationSentAt`, `MorningReminderSentAt`, `BeforeVisitReminderSentAt`, `AfterVisitSentAt`), also when
WhatsApp refused it. The job runs every 5 minutes and is switched on from the CMS Night Jobs panel (`JobSettings` row `VisitMessages`,
seeded OFF): switch it on after the templates below are approved. The language is the one the patient booked in
(`Appointments.PatientLanguage`; the NLP router sends it). Hinglish templates are Roman Hindi registered under language `en`.

## Templates to create in Meta (category UTILITY, named body variables)

Buttons (same in every template except `visit_after_*`, which has only the second):
1. URL button "Start navigation" / "Rasta dekhein" / "रास्ता देखें": `https://www.google.com/maps/dir/?api=1&destination={{1}}`
2. URL button "Visit details" / "Visit ki jaankari" / "विज़िट की जानकारी": `https://doctordekho.nexeagle.com/appointments/visit/{{1}}`

**visit_confirmation_en** (patient_name, doctor_name, hospital_name, when, booking_id, fee, directions)
> Hi {{patient_name}}, your visit with {{doctor_name}} at {{hospital_name}} on {{when}} is requested. Booking ID: {{booking_id}}. Fee:
> {{fee}}, paid at the hospital. The hospital will confirm the exact time. {{directions}}. Bring old prescriptions, reports and an ID.

**visit_confirmation_hinglish**
> Namaste {{patient_name}}, {{doctor_name}} ke saath {{hospital_name}} mein {{when}} ki visit request ho gayi hai. Booking ID:
> {{booking_id}}. Fees: {{fee}}, hospital mein dein. Sahi samay hospital confirm karega. {{directions}}. Purane parche, reports aur ID saath layein.

**visit_confirmation_hi**
> नमस्ते {{patient_name}}, {{doctor_name}} के साथ {{hospital_name}} में {{when}} की विज़िट का अनुरोध हो गया है। बुकिंग आईडी: {{booking_id}}।
> फ़ीस: {{fee}}, अस्पताल में दें। सही समय अस्पताल कन्फ़र्म करेगा। {{directions}}। पुराने पर्चे, रिपोर्ट और आईडी साथ लाएँ।

**visit_reminder_morning_*** (patient_name, doctor_name, hospital_name, when, directions)
> en: Good morning {{patient_name}}. Today: {{doctor_name}} at {{hospital_name}}, {{when}}. {{directions}}. Tap Start navigation when you leave.
> hinglish: Good morning {{patient_name}}. Aaj: {{doctor_name}}, {{hospital_name}}, {{when}}. {{directions}}. Nikalte waqt "Rasta dekhein" dabayein.
> hi: सुप्रभात {{patient_name}}। आज: {{doctor_name}}, {{hospital_name}}, {{when}}। {{directions}}। निकलते समय "रास्ता देखें" दबाएँ।

**visit_reminder_soon_*** (same variables)
> en: {{patient_name}}, your visit with {{doctor_name}} at {{hospital_name}} is at {{when}}. {{directions}}. When you reach, open Visit details to check in.
> hinglish: {{patient_name}}, {{doctor_name}} ke saath aapki visit {{when}} par hai, {{hospital_name}}. {{directions}}. Pahunch kar "Visit ki jaankari" se check in karein.
> hi: {{patient_name}}, {{doctor_name}} के साथ आपकी विज़िट {{when}} पर है, {{hospital_name}}। {{directions}}। पहुँचकर "विज़िट की जानकारी" से चेक-इन करें।

**visit_leave_now_*** (patient_name, doctor_name, hospital_name, when, directions) - same two buttons
> en: {{patient_name}}, it's time to leave for {{doctor_name}} at {{hospital_name}} ({{when}}). {{directions}}. Tap Start navigation for the way; reply to Vita if you'll be late.
> hinglish: {{patient_name}}, {{hospital_name}} mein {{doctor_name}} ({{when}}) ke liye nikalne ka samay ho gaya. {{directions}}. Rasta ke liye "Rasta dekhein" dabayein; late ho to Vita ko batayein.
> hi: {{patient_name}}, {{hospital_name}} में {{doctor_name}} ({{when}}) के लिए निकलने का समय हो गया। {{directions}}। रास्ते के लिए "रास्ता देखें" दबाएँ; देर हो तो Vita को बताएँ।

**visit_after_*** (patient_name, doctor_name)
> en: Hope you are feeling better, {{patient_name}}. How was your visit with {{doctor_name}}? Rate it, or book the follow-up the doctor asked for.
> hinglish: Ummeed hai aap behtar mehsoos kar rahe hain, {{patient_name}}. {{doctor_name}} ke saath visit kaisi rahi? Rating dein, ya doctor ka bataya follow-up book karein.
> hi: उम्मीद है आप बेहतर महसूस कर रहे हैं, {{patient_name}}। {{doctor_name}} के साथ विज़िट कैसी रही? रेटिंग दें, या डॉक्टर का बताया फ़ॉलो-अप बुक करें।

`directions` is the hospital's landmark, entrance and OPD location from its patient visit guide, or "-".

## Endpoints
- `GET/PUT /hospitals/{hospitalId}/visit-guide` (1HMS portal; members read, `admin_panel` admins save).
- `GET /public/appointments/{id}/visit`: where and when, guide, map link, fee, `canCheckIn`, `token` (number, position, expected wait),
  `followUpDate`. Check-in itself is the existing `POST /public/tokens` (appointment id + the patient's location, geofenced).

## Journey: getting the patient there (Vita "takes you there")
Table `AppointmentJourneys` (one row per appointment). No live location is sent or kept: only the travel mode, the leave time, the
expected arrival and the distance left. Reception sees a journey only when the patient agreed (`ShareWithHospital`).

- `PUT /public/appointments/{id}/journey` `{travelMode: drive|auto_cab|transit|walk, leaveAt: "08:15" | "2026-10-26T08:15", shareWithHospital}`:
  fields left out keep their value. The leave time is hospital time, on the appointment day or the day before, before the appointment.
  A new leave time gets its own "time to leave" message.
- `POST /public/appointments/{id}/journey/progress` `{status: on_the_way|late|arrived|cancelled, etaMinutes, distanceKm, lateByMinutes}`:
  on_the_way sets `startedAt` once and `etaAt = now + etaMinutes`; an ETA more than 5 minutes past the appointment time turns it `late`
  with `lateByMinutes` worked out; `late` with only `lateByMinutes` sets the arrival to the appointment time plus that; `arrived` is final
  (later updates are ignored, except cancel). Only within a day of the appointment.
- `GET /public/appointments/{id}/visit` carries `journey` (with `etaMinutes` from now while on the way).
- `GET /appointments/patient-appointment-details` (the appointment board) carries `journeyStatus`, `journeyTravelMode`,
  `journeyPlannedLeaveAt`, `journeyEtaAt`, `journeyLateByMinutes`, `journeyDistanceKm`, `journeyUpdatedAt` for shared journeys.
