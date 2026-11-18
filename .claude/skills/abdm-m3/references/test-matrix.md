# M3 test case matrix

Every case in NHA's M3 workbook, in the order the sheet groups them, with its marking, its Expected Result and the APIs the sheet lists for it. The sheet merges one APIs cell across the cases of a group, so a case lists every API of its group, and may call only some of them. Before building, read it and build every journey that a case mandatory for your entity type needs. Build each mandatory case's Expected Result into the screen, not only its calls. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 16 cases; 16 mandatory for at least one entity type.

An operation in another module's specification is named with that module. An unresolved path is one the sheet names that no operation in the specifications matches, with the reason: work out the call from the Expected Result and the specification.

- Journeys the mandatory cases need for a government entity: `m3-consent-management-data-flow-hiu`.
- Journeys the mandatory cases need for a private entity: `m3-consent-management-data-flow-hiu`.

## 1. Create Consent Request

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HIU_FLOW_102` Consent Request Initiation | mandatory | `m3_post_consent_v3_request_init`, `m3_post_consent_v3_request_status`, `m3_post_consent_v3_request_hiu_on_notify`, `m3_post_consent_v3_fetch`, `m3_post_data_flow_v3_health_information_request`, `m3_post_data_flow_v3_health_information_notify` | `m3-consent-management-data-flow-hiu` | `/sandbox/v3/new-documentation/swagger`: a link to a documentation page, which names no call | 1. In Consent Request Form:- Consent Request raised with the following details :- ABHA Address, Health Info Type , Purpose of Request , Health Info from, Health Info to , Consent Expiry. 2. Verify the all entered data (Name, ABHA Address, ABHA Number, HI types, Request Status, Cosnent Created On, Consent Expiry On, From Date ad To Date) are accurately viisbile in HIU with the status "Requested" and Requested Date and Time. |
| `HIU_FLOW_103` Notification to PHR | mandatory | `m3_post_consent_v3_request_init`, `m3_post_consent_v3_request_status`, `m3_post_consent_v3_request_hiu_on_notify`, `m3_post_consent_v3_fetch`, `m3_post_data_flow_v3_health_information_request`, `m3_post_data_flow_v3_health_information_notify` | `m3-consent-management-data-flow-hiu` | `/sandbox/v3/new-documentation/swagger`: a link to a documentation page, which names no call | 1. Beneficary's is successfully login with the same ABHA address in PHR app for which the consent request has been raised in HIU 2. Successfully verified that the consent request notification has been successfully get in PHR app. 3. Once the consent request notification opened in PHR then it shows below follwing details. Requester Name:- HIU Name , Purpose of Request , Information Request Duration, Information Request Type , Consent Valid Up To. 4. Consent Request Edit Scenario: Use can edit the consent request by the, the edited information is reflected in the HIU portal with all informations. |
| `HIU_FLOW_104` Listing of Consent Requests | mandatory | `m3_post_consent_v3_request_init`, `m3_post_consent_v3_request_status`, `m3_post_consent_v3_request_hiu_on_notify`, `m3_post_consent_v3_fetch`, `m3_post_data_flow_v3_health_information_request`, `m3_post_data_flow_v3_health_information_notify` | `m3-consent-management-data-flow-hiu` | `/sandbox/v3/new-documentation/swagger`: a link to a documentation page, which names no call | 1. After consent request initaited , the request is visbile in HIU Protal with the following details. ABHA Address, Health Info Type , Purpose of Request , Health Info from, Health Info to , Consent Expiry, and verify all the entered data (Name, ABHA Address, ABHA Number, HI types, Request Status, Cosnent Created On, Consent Expiry On, From Date ad To Date) are accurately viisbile in HIU with the status. 2. Date and time for all status is updated according to the status. 3. All editing information should be correctly visibile in HIU. 4. All Consent request according to their status(Granted, Expired, Denied, Revoked) should be visible with accurate time and status. |

## 3. Consent Request Denied

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HIU_FLOW_105` Consent Request is Denied | mandatory | none | none | none | 1.The status of the consent on the HIU portal should change to "Denied". 2. The timestamp of denial should be updated accurately. 3. No health data should be accessible or visible to the HIU for this request. |

## 3. Consent Request Granted

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HIU_FLOW_106` Consent Request is Granted | mandatory | none | none | none | 1. The consent status should change to "Granted" on the HIU portal. 2. The timestamp of when the consent was granted should be correctly logged and displayed. 3. If the consent was edited before granting, the updated parameters (HI types , from date, to date , consent expiry etc.) should reflect on the HIU portal. 4. Health data for the granted HI types should be visible and accessible to the HIU after the consent is granted. 5. No data should be visible to the HIU from any other HI types that were not granted in the consent request. |
| `HIU_FLOW_107` HIU_FLOW_107 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_108` HIU_FLOW_108 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_109` HIU_FLOW_109 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_110` HIU_FLOW_110 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_111` HIU_FLOW_111 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_112` HIU_FLOW_112 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_113` HIU_FLOW_113 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |
| `HIU_FLOW_114` HIU_FLOW_114 | government: optional; private: mandatory | none | none | none | HIU user should be able to view the health data of selected HI type from HIU application. |

## 4. Consent Request Expired

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HIU_FLOW_301` Consent Request is expired | mandatory | none | none | none | 1. The consent status should automatically update to "Expired" on the HIU portal. 2. The expiry timestamp should be accurately recorded and shown. 3. No health data should be accessible or retrievable for the expired consent. |

## 5. Consent Request Revoked

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HIU_FLOW_201` Revoke Consent (Patient PHR App) | mandatory | none | none | none | Verifiy that the patient PHR app allows revoking consent for individual HIPs. After revocation, the HIU is unable to access the health records. Revoked request appears in the consent list with proper status and timestamp. |
| `HIU_FLOW_202` Revoke Consent | mandatory | none | none | none | Verify that revoked consents are correctly marked in the HIU system. Access to associated health records is blocked post-revocation. Consent list is updated in real-time to reflect the new status. |
