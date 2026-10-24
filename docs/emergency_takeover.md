# Emergency takeover (Vita, vision Phase 3)

When Vita's emergency check fires, the patient sees the nearest emergency-ready places (nearest first, call buttons, 112 on top), and
those places get a "possible emergency nearby" alert. Tables: 1HMSDatabase `create_tables_emergency_takeover.sql`.

## Who appears
- A 1HMS hospital that opted in: **1HMS portal → Settings → Emergency takeover** (`/hospitals/{hospitalId}/emergency-settings`, admin only to save).
- A place not on 1HMS that a CMS admin added (district hospital, ambulance service).
- Only while switched on **and** verified by a CMS admin. A hospital that changes its numbers, map pin, alert number or hours goes back to
  PENDING (hidden) until checked again. Switching on needs the current takeover terms accepted (`EmergencyRules.TermsVersion`) and a
  WhatsApp number for alerts.

## Ranking (`EmergencyRules.Rank`)
Open now only (24x7 or the given hours, India time, overnight hours allowed). Straight-line distance from the patient's point. Everything
within 25 km; if fewer than 3, the nearest beyond 25 km until there are 3. A place with the capability asked for (`cardiac`, `maternity`,
`paediatric`, `trauma`, `icu`, `casualty`, `ambulance`) moves ahead of places at most 2 km nearer. At most 5.

## Endpoints
| | Endpoint | Notes |
|---|---|---|
| Public | `GET /public/emergency-places?lat=&lng=&capability=&take=5` | open like the rest of /public |
| Public | `POST /public/emergency-alerts` `{latitude, longitude, reason, capability?, channel, sessionRef}` | **X-Api-Key required**; 4 per 10 min per visitor IP (`EmergencyAlertPolicy`); one alert per chat session per 30 min (`repeated: true`, nothing sent again). Returns `eventId` and the places |
| Public | `POST /public/emergency-alerts/{eventId}/share-number` `{mobile, consent: true}` | X-Api-Key required; once per event, within 2 h; sends the number to the alerted places |
| Public | `POST /public/emergency-alerts/{eventId}/call` `{settingId}` | audit of a call tap (only places in that event) |
| Hospital | `GET/PUT /hospitals/{hospitalId}/emergency-settings` | JWT; members read, `admin_panel` saves |
| CMS | `GET /internal/emergency/places?status=`, `POST /internal/emergency/places/{id}/verify` `{decision, note}`, `POST/PUT /internal/emergency/places[/{id}]`, `GET /internal/emergency/alerts` | internal key + X-Actor |

Callers behind a server (the NLP router, Doctor Dekho) must forward the visitor's IP with `X-Internal-Proxy-Secret` +
`X-Forwarded-Client-Ip`, or every patient shares one 4-per-10-minutes budget.

## What is stored
Every alert, call tap and shared number (`EmergencyAlerts`, grouped by `EventId`). The patient's point is never stored, only the distance to
each place. The patient's number only when they ticked "Share my number so the hospital can call me". In 1HMS each alert is also a
CRITICAL in-app alert (`EMERGENCY_NEARBY`) for Admin, AdminDoctor, Doctor, Nurse and Receptionist; acknowledging it there shows in the CMS log.

## WhatsApp templates to approve in Meta (language `en`, named variables)
- `emergency_nearby_alert`: place_name, reason, distance, time. Suggested body: *"{{place_name}}: possible emergency ({{reason}}) detected
  {{distance}} from you at {{time}} via Doctor Dekho. The patient was shown your emergency number. Please be ready to receive them."*
- `emergency_patient_number`: place_name, reason, patient_mobile. Suggested body: *"{{place_name}}: the patient with a possible emergency
  ({{reason}}) asked you to call them on {{patient_mobile}}."*

Until approved (or while `WhatsApp:IsEnabled` is off) only the in-app 1HMS alert goes out; the log shows `WhatsAppSent = false`.

## Open before launch
Legal review of the takeover terms and the patient consent wording (vision open questions).
