# P1 test case matrix

Every case in NHA's P1 workbook, in the order the sheet groups them, with its marking and its Expected Result. Before building, read it and build each mandatory case's Expected Result for your entity type into the app screen. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 69 cases; 63 mandatory for at least one entity type.

NHA's PHR sheet has no APIs column, so no case names its calls. Read the P specification and journeys for the calls a case's steps imply.

## 1. ABHA address creation with mobile number flow (Self Declared Flow - without KYC)

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_101` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Not stated. |
| `PHR_ADDR_102` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Not stated. |
| `PHR_ADDR_103` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Not stated. |
| `PHR_ADDR_104` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Mobile OTP shall receive on the entered mobile number to vaidate mobile no for creating ABHA address |
| `PHR_ADDR_105` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Resend mobile OTP after 60 seconds, if first OTP is not yet received in an individual's mobile number. Vaidate mobile no to create ABHA address |
| `PHR_ADDR_106` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | PHR app should search based on mobile number linked with ABHA address and display all such ABHA addresses to select any one and login |
| `PHR_ADDR_107` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Not stated. |
| `PHR_ADDR_108` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Display both mandatory and optional profile details to create self-declared ABHA address |
| `PHR_ADDR_109` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | When an individual tries to create ABHA address for first time. Post clicking on checkbox beside user information agreement, an individual is able to register successfully. |
| `PHR_ADDR_110` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Same entered ABHA address is successfully created to login in PHR app. |
| `PHR_ADDR_112` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Password is successfully created to login in PHR app using login via ABHA address flow |
| `PHR_ADDR_113` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | An individual should able to login with updated ABHA address and password. |
| `PHR_ADDR_114` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | mandatory | Post agreeing to "Personal Data Processing Consent Form", an individual can login. |
| `PHR_ADDR_111` Registration Flow : ABHA address creation with mobile number flow (Self Declared Flow - without KYC) | optional | ABHA addresses are suggested as per an individual's name, username of e-mail ID |

## 2. Registration Flow : ABHA address creation with ABHA number flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_201` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Not stated. |
| `PHR_ADDR_202` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Not stated. |
| `PHR_ADDR_203` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Not stated. |
| `PHR_ADDR_204` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Aadhaar OTP/mobile OTP shall receive on the mobile no linked with entered ABHA no. Vaidate ABHA no for creating ABHA address |
| `PHR_ADDR_205` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Resend Aadhaar OTP / mobile OTP after 60 seconds, if first OTP is not yet received in an individual's mobile no. Vaidate ABHA no to create ABHA address |
| `PHR_ADDR_206` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | PHR app should search based on ABHA no linked with ABHA address and display all such ABHA addresses to select any one in which an individua wishes to login |
| `PHR_ADDR_207` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Not stated. |
| `PHR_ADDR_208` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Display both mandatory and optional profile details to create KYC verified ABHA address |
| `PHR_ADDR_209` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | When an individual tries to lcreate ABHA address for first time. Post clicking on checkbox beside user information agreement, an individual is able to successfully register in PHR app. |
| `PHR_ADDR_210` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Same entered ABHA address is successfully created to login in PHR app. |
| `PHR_ADDR_212` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Password is successfully created to login in PHR app using login via ABHA address flow |
| `PHR_ADDR_213` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | An individual should able to login with updated ABHA address and password. |
| `PHR_ADDR_214` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | mandatory | Post agreeing to "Personal Data Processing Consent Form", an individual can login. |
| `PHR_ADDR_211` Registration Flow : ABHA address creation with ABHA number flow (KYC verified flow) | optional | ABHA addresses are suggested as per an individual's name & username of e-mail ID |

## 3. ABHA number creation with KYC

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_301` ABHA number creation with KYC such as Aadhaar, DL, etc. Suppose an individual is creating ABHA number using Aadhaar as KYC | mandatory | Not stated. |
| `PHR_ADDR_302` PHR_ADDR_302 | mandatory | Not stated. |
| `PHR_ADDR_303` PHR_ADDR_303 | mandatory | Not stated. |
| `PHR_ADDR_304` PHR_ADDR_304 | mandatory | Aadhaar no is successfully validated using Aadhaar OTP |
| `PHR_ADDR_305` PHR_ADDR_305 | mandatory | Resend Aadhaar OTP after 60 seconds, if first OTP is not yet received in an individual's mobile no linked with Aadhaar. Vaidate ABHA no to create ABHA address |
| `PHR_ADDR_306` PHR_ADDR_306 | mandatory | Not stated. |
| `PHR_ADDR_307` PHR_ADDR_307 | mandatory | Not stated. |
| `PHR_ADDR_308` PHR_ADDR_308 | mandatory | ABHA number is created using Aadhaar demographic details as KYC |
| `PHR_ADDR_309` Second Flow Another Mobile no | mandatory | Not stated. |
| `PHR_ADDR_310` PHR_ADDR_310 | mandatory | Not stated. |
| `PHR_ADDR_311` PHR_ADDR_311 | mandatory | Not stated. |
| `PHR_ADDR_312` PHR_ADDR_312 | mandatory | Not stated. |
| `PHR_ADDR_313` PHR_ADDR_313 | mandatory | Not stated. |
| `PHR_ADDR_314` PHR_ADDR_314 | mandatory | Same entered ABHA address is successfully created to login in PHR app. |
| `PHR_ADDR_316` PHR_ADDR_316 | mandatory | ABHA number is created using Aadhaar demographic details as KYC |
| `PHR_ADDR_315` PHR_ADDR_315 | optional | ABHA addresses are suggested as per an individual's name & username of e-mail ID |

## 4.1. Login with mobile number flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_402` Login with mobile number flow | mandatory | Not stated. |
| `PHR_ADDR_403` Login with mobile number flow | mandatory | An individual shall validate entered mobile number by entering mobile OTP. Vaidate mobile no to login in PHR app |
| `PHR_ADDR_404` Login with mobile number flow | mandatory | Resend mobile OTP after 60 seconds, if first mobile OTP is not yet received to an individual's mobile.Vaidate mobile no to login in PHR app |
| `PHR_ADDR_405` Login with mobile number flow | mandatory | PHR app should search based on mobile number linked with ABHA address and display all such ABHA addresses to select any one and login |

## 5. Login with e-mail ID flow (Optional )

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_501` Login with e-mail ID flow | optional | An individual shall validate entered e-mail ID by entering e-mail OTP. Vaidate e-mail ID to login in PHR app |
| `PHR_ADDR_502` Login with e-mail ID flow | optional | Resend e-mail OTP after 60 seconds, if first e-mail OTP is not yet received to an individual's e-mail ID.Vaidate e-mail ID to login in PHR app |
| `PHR_ADDR_503` Login with e-mail ID flow | optional | PHR app should search based on e-mail ID linked with ABHA address and display all such ABHA addresses to select any one and login |

## 6. Login with easy to remember ABHA address flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_601` Login with easy to remember ABHA address (name@abdm) flow | mandatory | Not stated. |
| `PHR_ADDR_602` Login with easy to remember ABHA address (name@abdm) flow | mandatory | Post validation of ABHA address via password, an individual is able to successfully login |
| `PHR_ADDR_603` Login with easy to remember ABHA address (name@abdm) flow | mandatory | Post validation of ABHA address via OTP, an individual is able to successfully login |
| `PHR_ADDR_604` Login with easy to remember ABHA address (name@abdm) flow | mandatory | Resend Aadhaar OTP / mobile OTP/e-mail OTP after 60 seconds, if first OTP is not yet received.Vaidate ABHA address via Aadhaar OTP / mobile OTP/e-mail OTP to login in PHR app |

## 7. Login with default ABHA address flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_701` Login with default ABHA address (14-digit@abdm) flow | mandatory | Not stated. |
| `PHR_ADDR_702` Login with default ABHA address (14-digit@abdm) flow | mandatory | Post validation of ABHA address, an individual is able to successfully login |
| `PHR_ADDR_703` Login with default ABHA address (14-digit@abdm) flow | mandatory | Resend Aadhaar OTP / mobile OTP after 60 seconds, if first OTP is not yet received.Vaidate ABHA address via Aadhaar OTP / mobile OTP to login in PHR app |

## 8. Login with ABHA number flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_801` Login with ABHA number flow | mandatory | Not stated. |
| `PHR_ADDR_802` Login with ABHA number flow | mandatory | Post validation of ABHA number, an individual is able to successfully login |
| `PHR_ADDR_803` Login with ABHA number flow | mandatory | Resend Aadhaar OTP / mobile OTP after 60 seconds, if first OTP is not yet received.Vaidate ABHA number via Aadhaar OTP / mobile OTP to login in PHR app |

## 9. Login with Aadhaar number flow

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_901` Login with Aadhaar number flow | mandatory | Not stated. |
| `PHR_ADDR_902` Login with Aadhaar number flow | mandatory | Post validation of Aadhaar number, an individual is able to successfully login |
| `PHR_ADDR_903` Login with Aadhaar number flow | mandatory | Resend Aadhaar OTP / mobile OTP after 60 seconds, if first OTP is not yet received.Vaidate ABHA number via Aadhaar OTP / mobile OTP to login in PHR app |

## 10. Reset Password

| Case | Marking | Expected Result |
| --- | --- | --- |
| `PHR_ADDR_1001` Reset Password | mandatory | Post login in PHR app with any of the modes. An individual can reset the password. |
| `PHR_ADDR_1002` Reset Password | mandatory | Not stated. |
| `PHR_ADDR_1003` Reset Password | mandatory | Password is created successfully as per password policy. |
| `PHR_ADDR_1004` Reset Password | mandatory | Confirm the same password by re-entering it again to create the password |
| `PHR_ADDR_1005` Reset Password | mandatory | Post creation of new password, password shall change and an individual can login with new password. |
