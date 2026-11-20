# P2 test case matrix

Every case in NHA's P2 workbook, in the order the sheet groups them, with its marking and its Expected Result. Before building, read it and build each mandatory case's Expected Result for your entity type into the app screen. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 74 cases; 66 mandatory for at least one entity type.

NHA's PHR sheet has no APIs column, so no case names its calls. Read the P specification and journeys for the calls a case's steps imply.

## 1. Health and Facility records linking: User initiated linking flow (Discovery Flow) - Complete Discovery of HIP, linking, fetching and viewing of records in PHR app

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_101` Link HIP | mandatory | Patient will be directed to next screen to search HIP after clicking on "Link my Health Records" tab / "+" symbol in "Linked Facility" tab |
| `PHR_APP_102` Discover HIP | mandatory | Patient should be able to search ABDM compliant facility using PHR app |
| `PHR_APP_103` Display of patient details | mandatory | Details of HIP should be visible once individual search for HIP |
| `PHR_APP_104` Fill Patient ID (Optional) | mandatory | Patient can use this in customized way as per search |
| `PHR_APP_105` Matching of API specifications to link record | mandatory | Already linked care-context should not be discovered in the response. Linked care context will be discovered in "Linked Facilty" tab |
| `PHR_APP_106` Link records | mandatory | Display of all correct details of records for the purpose of linking Only the selected record is linked and not selected one is not linked |
| `PHR_APP_108` Linked facility will be visible in "Linked Facility" tab. Click on "Pull Records" button to fetch and view records. | mandatory | Health-records should be fetched in the app. Health-records should be fetched in the app. |
| `PHR_APP_107` Fill mobile OTP received to complete linking of record . | optional | 1. OTP will be received when individual clicks on fetch records, after matching of API specifications. 2. Specific error message is displayed as mentioned in test summary / steps |
| `PHR_APP_109` Display message regarding fetching of records may take time in "My Records" tab | optional | 1. Display message when an individual hover over I button 2. Status message should be displayed in case records takes time to fetch |
| `PHR_APP_110` Records will be displayed in "My Records" tab. Click on the attached report to view the health record. | optional | Health record details should be visible |
| `PHR_APP_111` View record in mobile device | optional | Records should open |

## 2. Health Programme records linking, fetching and viewing of records: User initiated linking (Discovery Flow)

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_201` Link Programme | mandatory | Patient will be directed to next search screen |
| `PHR_APP_202` Select Health Programme | mandatory | Select integrated government health programme with the PHR app. |
| `PHR_APP_203` Display of patient details | mandatory | Patient details should be visible after selecting government health programme in PHR app |
| `PHR_APP_204` Fill PM-JAY ID (Optional) | mandatory | Patient can use this in customized way as per search |
| `PHR_APP_205` Matching of API specifications | mandatory | Already linked care-context should not be discovered in the response Linked care context will be discovered in "Linked Facilty" tab |
| `PHR_APP_206` Link records | mandatory | Display of all correct details of records for the purpose of linking Only the selected record is linked and not selected one is not linked |
| `PHR_APP_208` Linked programme will be visible in "Linked Facility" tab. Click on "Pull Records" button to fetch and view records | mandatory | Health-records should be fetched in the PHR mobile app. Health-records should be fetched in the PHR mobile app. |
| `PHR_APP_207` Fill mobile OTP received to complete linking of record . | optional | 1. OTP will be received when individual clicks on fetch records, after matching of API specifications. 2. Specific error message is displayed as mentioned in test summary / steps 1. OTP will be received when individual clicks on fetch records, after matching of API specifications. 2. Specific error message is displayed as mentioned in test summary / steps 1. OTP will be received when individual clicks on fetch records, after matching of API specifications. 2. Specific error message is displayed as mentioned in test summary / steps |
| `PHR_APP_209` Display message regarding fetching of records may take time in "My Records" tab | optional | 1. Display message when an individual hover over I button 2. Status message should be displayed in case records takes time to fetch |
| `PHR_APP_210` Records will be displayed in "My Records" tab. Click on the attached report to view the health record. | optional | Health record details should be visible |
| `PHR_APP_211` View record in mobile device | optional | Records should open |

## HIP initiated linking flow - When health records are linked with ABHA address at the health facility / health programme.

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_300` Post linking of health records with ABHA address by the HIP with patient consent, the individual can view the health records on the PHR mobile app | mandatory | Health records should be linked, fetched and viewed in PHR app |

## Sharing of health records with patient's consent to the HIU

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_400` HIU should be able to view the health data of a APPROVED consent request | mandatory | HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. HIU individual should be able to view the health data of selected HI type from PHR app or HIU application. https://www.nrces.in/ndhm/fhir/r4/StructureDefinition-InvoiceRecord.html |

## 5.1. Scan QR Code using PHR app for sharing patient profile to HIP

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_50101` HIP scans the QR code from patient's PHR app | mandatory | HIP/HIU should be able to scan the QR code within 10 seconds. Thereafter patient can see profile details, consent language and then decide to either share or cancel the profile details. |
| `PHR_APP_50102` HIP scans the QR code from patient's PHR app | mandatory | Patient profile details in PHR app are shared successfully with the HMIS of HIP/HIU with patient's consent. |
| `PHR_APP_50103` Individual scans the HIP QR code at the facilty registration counter using PHR app | mandatory | Consent is received on PHR app to share the details |

## 5.2. Scan QR Code using phone camera or any other 3rd party QR scanner for sharing patient profile to HIP

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_50201` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | Once the user selected the PHR application from the pop-up list, the PHR app will launch. |
| `PHR_APP_50202` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | Post selecting PHR app, user will be redirected to the login page. |
| `PHR_APP_50203` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | After the successful login, user will be redirected to the share profile page. |
| `PHR_APP_50204` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | Post clicking on "Share" button, user profile is successfully shared with the HIP and if user click on "Cancel" button then user profile is not shared with the HIP. |
| `PHR_APP_50205` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | Once the user selected application launches, |
| `PHR_APP_50206` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | User will be redirected to the share profile page. |
| `PHR_APP_50207` Scan QR Code using phone camera or any other 3rd party QR scanner Use Case 1: ABDM compliant PHR application is already installed in user's phone | mandatory | Post clicking on "Share" button, user profile is successfully shared with the HIP and if user click on "Cancel" button then user profile is not shared with the HIP. |
| `PHR_APP_50208` Scan QR Code using phone camera or any other 3rd party QR scanner. Use Case 2: ABDM compliant PHR application is not installed in user's phone. | mandatory | After scanning the QR code, the user sees a browser from where he/she would be redirected to a User Initiated Deep link web page. |
| `PHR_APP_50209` Scan QR Code using phone camera or any other 3rd party QR scanner. Use Case 2: ABDM compliant PHR application is not installed in user's phone. | mandatory | This webpage will show the list of all the ABDM compliant PHR applications, from where the user has an option to select any application of his choice. User will be redirected to the play store / app store to install the PHR app |
| `PHR_APP_50210` Scan QR Code using phone camera or any other 3rd party QR scanner. Use Case 2: ABDM compliant PHR application is not installed in user's phone. | mandatory | User is able to create ABHA address and Password. Then redirect to share profile page. |
| `PHR_APP_50211` Scan QR Code using phone camera or any other 3rd party QR scanner. Use Case 2: ABDM compliant PHR application is not installed in user's phone. | mandatory | Post clicking on "Share" button, user profile is successfully shared with the HIP and if user click on "Cancel" button then user profile is not shared with the HIP. |

## Display Profile Details with status as KYC Verified / Self-Declared

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_600` Showing individual's profile details in the PHR app | mandatory | Patient profile details are displayed in the PHR app with their status such as KYC verified and Self-Declared. |

## Download ABHA address card in the PDF format

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_700` Download ABHA address card | mandatory | Individual is able to share / download the ABHA address card in device and profile details are displayed in it. ABHA no, ABHA address and mobile no |

## 9. "Requests" Section within "Consents" tab of PHR app:

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_901` "Requests" section in the "Consents" tab of PHR app | mandatory | All request on which action is not yet taken by patient are displayed in "Requested". Patient health record should not be shared with HIU if patient has not acted on consent request received. |
| `PHR_APP_902` "Requests" section in the "Consents" tab of PHR app | mandatory | All request which are denied by patient are displayed in "Denied". Patient health record should not be shared with HIU if consent is denied by patient |
| `PHR_APP_903` "Requests" section in the "Consents" tab of PHR app | mandatory | All request which are expired by individual are displayed in "Expired". Patient health record should not be shared with HIU if consent is expired by patient |

## 10. "Approved" Section within "Consents" tab of PHR app:

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1001` "Approved" section in the "Consents" tab of PHR app | mandatory | All request which are granted by patient are displayed in "Granted". Patient health record are successfully shared with HIU if consent is granted by patient |
| `PHR_APP_1002` "Approved" section in the "Consents" tab of PHR app | mandatory | All request which are revoked by patient are displayed in "Revoked". Patient health record are successfully shared with HIU if consent is granted by patient. |

## 11. "My Records" tab : Fetching of health records into "My Records" tab , post linking of health records with ABHA address

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1101` View patient helth records in "My Records" tab of PHR app | mandatory | Record details are displayed after clicking on record |
| `PHR_APP_1102` View patient helth records in "My Records" tab of PHR app | mandatory | All details of record like HI type, Date, Time, Name of Doctor/Facilty are displayed |
| `PHR_APP_1103` View patient helth records in "My Records" tab of PHR app | mandatory | Able to view the health record |

## 12. "Linked Facility" tab: All linked providers (health locker, health facility and health programme) are listed in this tab.

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1201` "Linked Facility" tab in PHR app | mandatory | All linked providers are listed here. |

## Edit Subscription Request/Disable auto approval request

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1300` Edit active subscription request | mandatory | Changes in Active subscription requested are saved |
| `PHR_APP_1400` Disable auto approval requests | mandatory | Auto - approval policy can be disabled. Auto - approval policy can be disabled. |

## 15. Link ABHA no to the ABHA address

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1501` Link ABHA no to the ABHA address | mandatory | In case ABHA no is not linked with ABHA address - "Link ABHA number" option should be provided. |
| `PHR_APP_1502` Link ABHA no to the ABHA address | mandatory | Enter 14-digit ABHA no |
| `PHR_APP_1503` Link ABHA no to the ABHA address | mandatory | ABHA number is successfully validated by aadhar OTP / mobile OTP |
| `PHR_APP_1504` Link ABHA no to the ABHA address | mandatory | Resend OTP option is working |
| `PHR_APP_1505` Link ABHA no to the ABHA address | mandatory | Post OTP validation - a) Message is displayed informing an individual that ABHA no is linked with ABHA address b) ABHA number is visible in profile / home screen c) Profile details are changed as per ABHA number. d) "Self-Declared" status is changed to "KYC verified" in profile / home screen |
| `PHR_APP_1506` Link ABHA no to the ABHA address | mandatory | Post clicking on "Go back to home screen" tab, an individual is able to go back to home screen |

## 17. Edit profile for KYC verified profile

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1701` Edit profile for KYC verified profiles | mandatory | Post mobile OTP validation, mobile no is updated successfully. |
| `PHR_APP_1703` Edit profile for KYC verified profiles | mandatory | Fields such as address line 1, district, state and pin-code can be updated successfully. |

## 18. Edit profile for Self-Declared profile

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1801` Edit profile for Self-Declared profile | mandatory | Photo is updated successfully |
| `PHR_APP_1802` Edit profile for Self-Declared profile | mandatory | Name is updated successfully |
| `PHR_APP_1803` Edit profile for Self-Declared profile | mandatory | Gender is updated successfully |
| `PHR_APP_1804` Edit profile for Self-Declared profile | mandatory | DoB is update successfully |
| `PHR_APP_1805` Edit profile for Self-Declared profile | mandatory | Post mobile OTP validation, mobile no is updated successfully. |
| `PHR_APP_1807` Edit profile for Self-Declared profile | mandatory | Fields such as address line 1, district, state and pin-code can be updated successfully. |

## 19. Deep Link flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_1901` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Patient registers and create health record at facility but do not provide ABHA address for linking of records with ABHA address. |
| `PHR_APP_1902` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Health Facility is participating in ABDM, then it will notify ABDM when there is a new health record for this patient. |
| `PHR_APP_1903` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | ABDM will send SMS to patient along with the deep link, if health record is created but not linked with ABHA address |
| `PHR_APP_1904` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Post patient click on the deep link ABDM compliant PHR apps such as ABHA, Dreifcase, Raxa, Eka Care, Pay TM, Arogya Setu, etc. Going forward, other PHR app's will also add up to this list. User can select any one of the PHR app of his/her choice. |
| `PHR_APP_1905` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Install the selected PHR app successfully from the list. |
| `PHR_APP_1906` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | New user is able to create ABHA address and Password by any of the three registration flows (Registration via ABHA no, Registration via Mobile No, Registration via Email ID) |
| `PHR_APP_1907` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Launch the selected PHR app from the list |
| `PHR_APP_1908` Deep Link Flow: Send SMS to patient mobile to initiate record linking and fetching in PHR app | mandatory | Read above point 1 - (1.1 to 1.11) and check that record is fetched and viewed using User initiated linking (Discovery Flow) Patient is able to login in PHR app by any one of the mode (Login via mobile no, login via email ID, login via ABHA number, login via ABHA address) Post successful login in PHR app, patient is able to scan the QR Code at ABDM compliant health facility. Patient profile details such as ABHA number, ABHA address, Name, Date of Birth, Gender, Mobile No and Address are successfully shared at the HMIS side By sharing the profile details, patient has given the consent for registration and linking of health records and consent statement needs to be displayed before sharing of patient profile details to the HMIS side of the hospital. Display below notification in app: Successfully shared your details with <Hospiatal Name>. Your token number is <> Display token number for valid configurable set time duration. Patient is able to scan the QR Code at the ABDM compliant health facility using third party scanner / phone camera. Check that, post scanning the QR code with the third party scanner / phone camera, display installed ABDM compliant PHR apps and browser Check that post clicking on browser, display the list of all ABDM compliant PHR apps. Patient can choose any one of the PHR app and click on it. If the selected PHR app is not installed in mobile device, then patient is redirected to play store / app store to install the app or if the selected PHR app is already installed then patient can login and share profile to get token no. In this case, patient is already looged-in, so patient is redirected to share profile page of PHR app Patient profile details such as ABHA number, ABHA address, Name, Date of Birth, Gender, Mobile No and Address are successfully shared at the HMIS side By sharing the profile details, patient has given the consent for registration and linking of health records and consent statement needs to be displayed in the PHR app, before sharing of patient profile details to the HMIS side of the hospital. Display below notification in app: Successfully shared your details with <Hospiatal Name>. Your token number is <> Display token number for valid configurable set time duration. Patient is able to scan the QR Code at the ABDM compliant health facility using third party scanner / phone camera. Check that post scanning the QR code with the third party scanner / phone camera, display installed ABDM compliant PHR apps and browser Check that post clicking on browser, display the list of all ABDM compliant PHR apps. Patient can choose any one of the PHR app and click on it. If the selected PHR app is not installed in mobile device, then patient is redirected to play store / app store to install the app or if the selected PHR app is already installed then patient can login and share profile to get token no. In this case, patient is not looged-in, so patient needs to login into the PHR app via any one of the modes (Login via mobile no, login via email ID, login via ABHA number, login via ABHA address) Patient is redirected to share profile page of PHR app Patient profile details such as ABHA number, ABHA address, Name, Date of Birth, Gender, Mobile No and Address are successfully shared at the HMIS side |

## 20. Scan and Share

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_APP_2001` PHR_APP_2001 | mandatory | By sharing the profile details, patient has given the consent for registration and linking of health records and consent statement needs to be displayed in the PHR app, before sharing of patient profile details to the HMIS side of the hospital. Display below notification in app: Successfully shared your details with <Hospiatal Name>. Your token number is <> Display token number for valid configurable set time duration. Patient is able to scan the QR Code at the ABDM compliant health facility using third party scanner / phone camera. Display list of all ABDM compliant PHR apps (randomly) and patient can select any one of them which he/she wishes to install in mobile device. Patient is redirected to Google Play Store / app store to download / install the selected mobile PHR app Post installation, PHR app is launched. |
