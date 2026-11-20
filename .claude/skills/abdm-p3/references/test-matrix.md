# P3 test case matrix

Every case in NHA's P3 workbook, in the order the sheet groups them, with its marking and its Expected Result. Before building, read it and build each mandatory case's Expected Result for your entity type into the app screen. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 15 cases; 15 mandatory for at least one entity type.

NHA's PHR sheet has no APIs column, so no case names its calls. Read the P specification and journeys for the calls a case's steps imply.

## 1.1. Subscription Request

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_HIU_10101` Subscription Request receives in the PHR app | mandatory | Notification is received in the PHR app Subscription requests is received in the PHR app |
| `PHR_HIU_10102` "Grant" subscription request | mandatory | Notification is sent to HIU for creation of health record Subscription is provided to the HIU for selected "HI type", "type of visit" and "time period" |
| `PHR_HIU_10103` "Deny" subscription request | mandatory | No more notification is sent to HIU requesting health record from patient |
| `PHR_HIU_10104` "Expired" subscription request | mandatory | Subscription request is expired in 2 hours if no action (grant/deny) is taken by patient Subsciption is provided to HIU for set "time period" only |
| `PHR_HIU_10105` "Edit" subscription request | mandatory | After editing active subscription request and saving changes - Subscription is provided to the HIU for selected "HI type", "type of visit" and "time period" only |

## 1.2. Auto approval policy

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_HIU_10201` Set up auto approval policy - Enabled | mandatory | Patient do not have to manually approve consent request received from HIU for seamless patient experience Notification is sent to HIU whenever new record is created at HIP. History of all types of request received and action taken on it are seen in PHR app |
| `PHR_HIU_10202` Didn't set up auto approval policy | mandatory | Consent request is received in PHR app, whenever new record is created in HIP and record can be shared only if patient grant consent request |
| `PHR_HIU_10203` "Disabled" auto approval policy | mandatory | Auto approval of consent request should stop immediately and Consent request is received in PHR app, whenever new record is created in HIP. Record can be shared only if patient grant consent request |

## 1.3. Consent Request

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_HIU_10301` Notification is sent to hospital HIU | mandatory | Notification is sent to Hospital HIU whenever new record is created at HIP. |
| `PHR_HIU_10302` Notification is sent to PHR app | mandatory | Notification is sent to PHR app after HIU creates consent request |

## 1.4. Types of Request (Approved / Denied / Expired / Revoked)

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_HIU_10401` Approved: HIU should be able to view the health data of a APPROVED consent request | mandatory | HIU can fetch and view health data for the approved/granted consent request HIU can fetch and view health data for the approved/granted consent request |
| `PHR_HIU_10402` Denied: HIU should not be able to view the health data of a DENIED consent request | mandatory | HIU cannot fetch health data for the approved consent request |
| `PHR_HIU_10403` Expired : HIU should not be able to view the health data of a EXPIRED consent request | mandatory | HIU cannot fetch health data for the Expired/not acted upon consent requests HIU cannot fetch health data for the Expired/not acted upon consent requests |
| `PHR_HIU_10404` Revoked: HIU should not be able to view the health data of a REVOKED consent request | mandatory | HIU cannot fetch health data for the revoked consent request |

## 1.5. Fetching the consent artefact by the HIU

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_HIU_10501` Fetching the consent artefact by the HIU | mandatory | Correct health record should be seen against each HIP |
