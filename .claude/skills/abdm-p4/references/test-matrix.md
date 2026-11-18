# P4 test case matrix

Every case in NHA's P4 workbook, in the order the sheet groups them, with its marking and its Expected Result. Before building, read it and build each mandatory case's Expected Result for your entity type into the app screen. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 42 cases; 41 mandatory for at least one entity type.

NHA's PHR sheet has no APIs column, so no case names its calls. Read the P specification and journeys for the calls a case's steps imply.

## 1. Discovery & add health locker

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_101` Discovery & add health locker Linking locker with existing Health ID (ABHA address) | optional | Locker will be linked with the health id. A consent request will be sent to the PHR app for linking. Locker will be linked with the health id. A consent request will be sent to the PHR app for linking. Locker will be linked with the health id. A consent request will be sent to the PHR app for linking. Locker will be linked with the health id. A consent request will be sent to the PHR app for linking. |

## 2. Set up a Health Locker through PHR app

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_201` Linking Health ID with existing locker | mandatory | Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. Consent Notification will be sent to PHR app for confirming the Health ID. Token will be received which is a long term linking token and can be used multiple times. Locker will be linked with the health id. |

## 3. Fetching of health record from HIP to Locker

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_301` Notification is sent from HIP to locker | mandatory | Notification is received in locker. |
| `PHR_LOCKER_302` Locker Consent request is received in PHR app and SMS is also sent from locker to users mobile | mandatory | Records will be fetched into health locker once user approves the request |
| `PHR_LOCKER_303` The consent request is auto approved based on the policy | mandatory | Records will be auto fetched whenever new records will be available |
| `PHR_LOCKER_304` Grant consent received in "Request" list, of PHR app, so that locker can view health records from all linked PHR accounts | mandatory | User can view records in healthlocker upon approval of consent |

## 3.5

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_306` Once locker has the consent, it can fetch data from respective HIP | mandatory | Records from HIP's will be visible in locker |
| `PHR_LOCKER_307` After getting the record for a care context, the locker links it to CM | mandatory | Care context will be visible in PHR |
| `PHR_LOCKER_308` View health record received from HIP to locker | mandatory | Records can be viewed in locker once fetched |

## 4. Upload document in health locker through PHR app

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_400` User pushes health record to health locker by uploading the scanned reports through clicking on "Upload" icon in PHR app | mandatory | Records will be uploaded |
| `PHR_LOCKER_401` Upload Document Fields | mandatory | Not stated. |
| `PHR_LOCKER_402` Processing of the uploaded document in the health locker, to find that uploaded document is health record or not | mandatory | Process will be done at locker level |
| `PHR_LOCKER_403` Locker stores the record against care context | mandatory | Records will be stored |
| `PHR_LOCKER_404` Link the uploaded document to Health ID (ABHA address) by linking the care context on CM - HIP initiated linking | mandatory | Care context will be added |
| `PHR_LOCKER_405` After document is linked to Health ID (ABHA address), the locker is displayed in the "Providers" tab of PHR app | mandatory | Locker will be created in PHR |
| `PHR_LOCKER_406` Click on "Get Data" to fetch the record in the "Records" tab of PHR app | mandatory | Records will be fetched |
| `PHR_LOCKER_407` Click on the record received in the "Records" tab of PHR app - to see record details and open the attachment to view patient record | mandatory | Records will be viewed from attachments |

## 5.1. Subscription Request - Getting to know about new health record

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_50101` Locker / HIU will raise a request to subscribe to notifications for patient records | mandatory | Subscription request will be set up |
| `PHR_LOCKER_50102` Patient will approve the subscription request in PHR app | mandatory | Request will be visible in PHR under request tab |
| `PHR_LOCKER_50103` Locker will be notified whenever there is new data / care context available for the patient | mandatory | Alert notification is received in locker |
| `PHR_LOCKER_50104` On notification, locker will fetch the new record (There may be a need to create a new consent request) | mandatory | Records will be fetched |

## 5.2. Auto approval policy - Simplifying user experience for consents

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_50201` Based on the subscription (if approved), user can choose to create an auto approval policy | mandatory | Consents will be auto approved |
| `PHR_LOCKER_50202` If a consent request is within the bounds of the policy, it will be automatically approved | mandatory | Consents will be auto approved |
| `PHR_LOCKER_50203` Disable auto approval policy | mandatory | Consents needs to be manually approved |

## 5.3. Authorization Request - Letting the consent manager know Locker has records

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_50301` Locker/HIP will raise a direct auth request | mandatory | Consent request will be there under request tab in PHR |
| `PHR_LOCKER_50302` User will have to approve it | mandatory | Request will be seen in PHR |
| `PHR_LOCKER_50303` Locker/HIP will get a linking token (as in the case on HIP initiated linking) | mandatory | Linking token can be checked |
| `PHR_LOCKER_50304` In case of locker, this token can be used to link multiple care context | mandatory | Multiple care context |

## 6. Viewing Consent Request

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_601` Viewing list of Consent Requests in "Requests" tab of PHR app | mandatory | Types of consents visible |
| `PHR_LOCKER_60101` 1) Requested - Not yet any action is taken by user on "Consent Requests" | mandatory | To act upon requests under requested |
| `PHR_LOCKER_60102` 2) Denied - User have "Denied" received consent request | mandatory | Rejected requests |
| `PHR_LOCKER_60103` 3) Expired - Consent Requests is expired | mandatory | Expired requests |

## 7. Viewing list of Consent Requests in the "Approved" tab of PHR app

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_70101` 1) Granted | mandatory | Granted |
| `PHR_LOCKER_70102` 2) Expired | mandatory | Expired |
| `PHR_LOCKER_800` Edit active subscription request | mandatory | Request will be edited |
| `PHR_LOCKER_900` Disable auto approval requests | mandatory | Needs manual approval |

## 10. Types of Consent in Health Locker

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_1001` Approved: HIU should be able to view the health data of a APPROVED consent request | mandatory | HIU user should be able to view the health data for the GRANTED consent request. |
| `PHR_LOCKER_1002` Denied: HIU should not be able to view the health data of a DENIED consent request | mandatory | HIU user should not be able to view the health data for the DENIED consent request. HIU user should not be able to view the health data for the DENIED consent request. |
| `PHR_LOCKER_1003` Expired : HIU should not be able to view the health data of a EXPIRED consent request | mandatory | HIU user should not be able to view the health data for the EXPIRED/NOT ACTED UPON consent request. HIU user should not be able to view the health data for the EXPIRED/NOT ACTED UPON consent request. |
| `PHR_LOCKER_1004` Revoked: HIU should not be able to view the health data of a REVOKED consent request | mandatory | HIU user should not be able to view the health data for the REVOKED consent request. |
| `PHR_LOCKER_1005` Auto Approval | mandatory | HIU user should be able to fetch the health data for the AUTO APPROVED consent request, HIU user should be able to fetch the health data for the AUTO APPROVED consent request, HIU user should be able to fetch the health data for the AUTO APPROVED consent request, |

## 11. HIU receives record only if consent request is approved in PHR app

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_LOCKER_1100` HIU should be able to view the health data of a APPROVED consent request | mandatory | HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. HIU user should be able to view the health data of selected HI type from PHR app or HIU application. |
