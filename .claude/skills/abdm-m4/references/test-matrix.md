# M4 test case matrix

Every case in NHA's M4 workbook, in the order the sheet groups them, with its marking, its Expected Result and the APIs the sheet lists for it. The sheet merges one APIs cell across the cases of a group, so a case lists every API of its group, and may call only some of them. Before building, read it and build every journey that a case mandatory for your entity type needs. Build each mandatory case's Expected Result into the screen, not only its calls. The test run uses it to know what each case exercises.

Marking is for a government and a private entity, as `abdm_report.py plan` reads the workbook. Mandatory cases come first in each group. 190 cases; 108 mandatory for at least one entity type.

An operation in another module's specification is named with that module. An unresolved path is one the sheet names that no operation in the specifications matches, with the reason: work out the call from the Expected Result and the specification.

- Journeys the mandatory cases need for a government entity: `m4-enrollment`, `m4-multiple-hrp-api`, `m4-onboarding-apis`, `m4-search`, `m4-utilities`.
- Journeys the mandatory cases need for a private entity: `m4-enrollment`, `m4-multiple-hrp-api`, `m4-onboarding-apis`, `m4-search`, `m4-utilities`.

## HFR Search facility: Facility search

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-002` search through facility name | conditional | none | none | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility name should get filled |
| `HFR-003` To fill the facility ownership | conditional | `m4_get_v1_5_facility_get_master_data`, `m4_post_v1_5_facility_get_owner_subtype` | `m4-utilities` | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility ownership should get filled |
| `HFR-004` To fill the facility state | conditional | `m4_get_v1_5_facility_lgd_states` | `m4-utilities` | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility state should get filled |
| `HFR-008` To show number of pages | mandatory | none | none | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | should have default value as 1 |
| `HFR-009` To show number of facilities per page | mandatory | none | none | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | should have default value as 10 |
| `HFR-001` Search through facility id | optional | none | none | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility id should get filled |
| `HFR-005` To fill the facility district | optional | `m4_get_v1_5_facility_lgd_districts` | `m4-utilities` | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility district should get filled |
| `HFR-006` To fill the facility sub-district | optional | `m4_get_v1_5_facility_lgd_subdistricts` | `m4-utilities` | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility subdistrictshould get filled |
| `HFR-007` To fill the facility pincode | optional | none | none | `v15SearchFacilitiesFuzzyPost`: a Swagger operation name that names no operation in the specifications | Facility pincode should get filled |

## HFR Registration: Facility Registration(Basic demographics of the facility)

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-010` To Save the facilty basic details | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Filled Facility Name |
| `HFR-011` Fill the geographic location | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Latitude has been filled as per the definition |
| `HFR-012` Fill the geographic location | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | longitude has been filled as per the definition |
| `HFR-013` To select the country | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Not stated. |
| `HFR-014` To select the State | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_states` | `m4-onboarding-apis`, `m4-utilities` | none | State should get selected |
| `HFR-015` To select the District | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_districts` | `m4-onboarding-apis`, `m4-utilities` | none | District should get selected |
| `HFR-016` To select the Sub-district | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_subdistricts` | `m4-onboarding-apis`, `m4-utilities` | none | Sub-District should get selected |
| `HFR-017` To fill the facility address | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address should get filled |
| `HFR-019` To enter the facility pincode | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Pincode should get filled |
| `HFR-020` To select the facility working days | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | working days should get selected |
| `HFR-021` To fill the opening hours of the facility | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Facility Timings should get saved |
| `HFR-022` To select the facility operational status | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Facility operational status should get filled |
| `HFR-030` facility file Uploads | conditional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address proof should get saved |
| `HFR-031` To provide the ownership details | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Facility ownership should get saved |
| `HFR-032` To provide the ownership details | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_data`, `m4_post_v1_5_facility_get_owner_subtype` | `m4-onboarding-apis`, `m4-utilities` | none | Facility ownership subtype should get saved |
| `HFR-033` To provide the ownership details | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Not stated. |
| `HFR-034` To provide the details of system of medicine | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | System of medicine should get saved |
| `HFR-035` To enter the type of facility | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_fetch_facility_type` | `m4-onboarding-apis`, `m4-utilities` | none | Facility Type should get saved |
| `HFR-036` To enter the type of facility | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_fetch_facility_sub_type` | `m4-onboarding-apis`, `m4-utilities` | none | Facility sub type should get saved |
| `HFR-037` To enter the type of services provided | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Multiple type of service should get selected |
| `HFR-038` To enter the Specialization | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_get_specialities` | `m4-onboarding-apis`, `m4-utilities` | none | Multiple Specialization should get selected |
| `HFR-018` To fill the facility address | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address should get filled |
| `HFR-023` To enter the facility landline number for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Landline number should get filled |
| `HFR-024` To enter the facility mobile number for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Mobile number should get filled |
| `HFR-025` To enter the email for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Facility email should get filled |
| `HFR-026` To enter the hospital website link | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Proper Website should get filled |
| `HFR-027` facility file Uploads | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Photograph should get saved |
| `HFR-028` facility file Uploads | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Photograph should get saved |
| `HFR-029` facility file Uploads | optional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Address proof type should get selected |

## HFR Registration: Facility Registration(Capture Health program ids)

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-039` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-040` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-041` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-042` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-043` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-044` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-045` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-046` Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |

## HFR Registration: Facility Registration(Capture facility details information-infrastructure and specialization

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-047` systemOfMedicineCode | mandatory | `m4_post_v1_5_facility_detailed_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | System of medicine should get selected |
| `HFR-048` isSpecializationAvalaible | mandatory | none | none | none | Not stated. |
| `HFR-049` specialities | conditional | `m4_post_v1_5_facility_detailed_information`, `m4_post_v1_5_facility_get_specialities` | `m4-onboarding-apis`, `m4-utilities` | none | Not stated. |
| `HFR-057` totalNumberOfVentilators | mandatory | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-060` totalNumberOfBeds | mandatory | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-050` countIPDBedsWithoutOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-051` countIPDBedsWithOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-052` countICUBedsWithVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-053` countICUBedsWithoutVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-054` countHDUBedsWithFunctionalVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-055` countHDUBedsWithVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-056` countHDUBedsWithoutVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-058` countDayCareBedsWithoutOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-059` countDayCareBedsWithOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-061` countDentalChairs | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |

## HFR Registration: Facility submission

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-062` sourceOfInformation | optional | `m4_post_v1_5_facility_submit_facility` | `m4-onboarding-apis` | none | Facility shold get submitted |
| `HFR-063` sourceUniqueID | optional | none | none | none | Not stated. |

## HFR Facility update: Facility Registration(Basic demographics of the facility)

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-064` To edit the facilty basic details | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Filled Facility Name |
| `HFR-065` Change the geographic location | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Latitude has been filled as per the definition |
| `HFR-066` Change the geographic location | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | longitude has been filled as per the definition |
| `HFR-067` To select the country | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Not stated. |
| `HFR-068` To edit the State | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_states` | `m4-onboarding-apis`, `m4-utilities` | none | State should get selected |
| `HFR-069` To edit the District | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_districts` | `m4-onboarding-apis`, `m4-utilities` | none | District should get selected |
| `HFR-070` To edit the Sub-district | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_lgd_subdistricts` | `m4-onboarding-apis`, `m4-utilities` | none | Sub-District should get selected |
| `HFR-071` To edit the facility address | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address should get filled |
| `HFR-073` To edit the facility pincode | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Pincode should get filled |
| `HFR-074` To edit the facility working days | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | working days should get selected |
| `HFR-075` To edit the opening hours of the facility | mandatory | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Facility Timings should get saved |
| `HFR-076` To edit the facility operational status | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Facility operational status should get filled |
| `HFR-084` edit facility file Uploads | conditional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address proof should get saved |
| `HFR-085` To edit the ownership details | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Facility ownership should get saved |
| `HFR-086` To edit the ownership details | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_data`, `m4_post_v1_5_facility_get_owner_subtype` | `m4-onboarding-apis`, `m4-utilities` | none | Facility ownership subtype should get saved |
| `HFR-087` To edit the ownership details | conditional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Not stated. |
| `HFR-088` To edit the details of system of medicine | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | System of medicine should get saved |
| `HFR-089` To edit the type of facility | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_fetch_facility_type` | `m4-onboarding-apis`, `m4-utilities` | none | Facility Type should get saved |
| `HFR-090` To edit the type of facility | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_fetch_facility_sub_type` | `m4-onboarding-apis`, `m4-utilities` | none | Facility sub type should get saved |
| `HFR-091` To edit the type of services provided | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Multiple type of service should get selected |
| `HFR-092` To edit the Specialization | mandatory | `m4_post_v1_5_facility_basic_information`, `m4_post_v1_5_facility_get_specialities` | `m4-onboarding-apis`, `m4-utilities` | none | Multiple Specialization should get selected |
| `HFR-072` To edit the facility address | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Address should get filled |
| `HFR-077` To eidt the facility landline number for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Landline number should get filled |
| `HFR-078` To edit the facility mobile number for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Mobile number should get filled |
| `HFR-079` To edit the email for public display | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Facility email should get filled |
| `HFR-080` To edit the hospital website link | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Proper Website should get filled |
| `HFR-081` edit facility file Uploads | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Photograph should get saved |
| `HFR-082` edit facility file Uploads | optional | `m4_post_v1_5_facility_basic_information` | `m4-onboarding-apis` | none | Photograph should get saved |
| `HFR-083` edit facility file Uploads | optional | `m4_post_v1_5_facility_basic_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | Address proof type should get selected |

## HFR Facility update: Facility Registration(Capture Health program ids)

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-093` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-094` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-095` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-096` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-097` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-098` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-099` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |
| `HFR-100` Edit Linked program Id | optional | `m4_post_v1_5_facility_additional_information` | `m4-onboarding-apis` | none | It should get filled |

## HFR Facility update: Facility Registration(Capture facility details information-infrastructure and specialization

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-101` To edit systemOfMedicine | mandatory | `m4_post_v1_5_facility_detailed_information`, `m4_get_v1_5_facility_get_master_types`, `m4_get_v1_5_facility_get_master_data` | `m4-onboarding-apis`, `m4-utilities` | none | System of medicine should get selected |
| `HFR-102` to edit if Specialization is Avalaible | mandatory | none | none | none | Not stated. |
| `HFR-103` To edit specialities | conditional | `m4_post_v1_5_facility_detailed_information`, `m4_post_v1_5_facility_get_specialities` | `m4-onboarding-apis`, `m4-utilities` | none | Not stated. |
| `HFR-111` To edit totalNumberOfVentilators | mandatory | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-114` To edit totalNumberOfBeds | mandatory | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-104` To edit countIPDBedsWithoutOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-105` To edit countIPDBedsWithOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-106` To edit countICUBedsWithVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-107` To edit countICUBedsWithoutVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-108` To edit countHDUBedsWithFunctionalVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-109` To edit countHDUBedsWithVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-110` To edit countHDUBedsWithoutVentilators | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-112` To edit countDayCareBedsWithoutOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-113` To edit countDayCareBedsWithOxygen | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |
| `HFR-115` To edit countDentalChairs | optional | `m4_post_v1_5_facility_detailed_information` | `m4-onboarding-apis` | none | Details should get filled |

## HFR Facility update: Facility submission

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-116` sourceOfInformation | optional | `m4_post_v1_5_facility_submit_facility` | `m4-onboarding-apis` | none | Facility shold get submitted |
| `HFR-117` sourceUniqueID | optional | none | none | none | Not stated. |

## HFR Bridge linkage: Bridge linkage

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HFR-118` Fill the facility Id | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Facility should get validated |
| `HFR-119` Fill the facility Name | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Facility Name should fetch |
| `HFR-120` Fill the bridge Id | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Bridge id should get validated |
| `HFR-121` Fill the hip Name | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Valid HIP name should get filled |
| `HFR-122` Fill the HIP type | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Not stated. |
| `HFR-123` Provide details if the bridge is Active or not | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Not stated. |
| `HFR-118#2` Fill the facility Id | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Facility should get validated |
| `HFR-119#2` Fill the facility Name | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Facility Name should fetch |
| `HFR-120#2` Fill the bridge Id | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Bridge id should get validated |
| `HFR-121#2` Fill the hip Name | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Valid HIP name should get filled and HIP name should not be repeated |
| `HFR-122#2` Fill the HIP type | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Not stated. |
| `HFR-123#2` Provide details if the bridge is Active or not | mandatory | `m4_post_v1_bridges_mutiplehrpaddupdateservices` | `m4-multiple-hrp-api` | none | Not stated. |

## HPR: Creation of HPR Profile

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HPR-002` Create your Healthcare Professional ID via Aadhaar | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | Not stated. |
| `HPR-003` consent collection | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | Please refer the cell no -M5 |
| `HPR-004` Suggestions:- Consent collection should be multilingual | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | User provides consent for sharing of Aadhaar details and creation of HPR. System records user consent for compliance with Health Data Management Policy. |
| `HPR-005` Aadhaar collection and Error Message | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | User is prompted to enter correct Aadhaar Number |
| `HPR-006` Captcha | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | Not stated. |
| `HPR-007` Aadhaar OTP Collection | mandatory | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | Not stated. |
| `HPR-008` Resend OTP | mandatory | none | none | none | User will able to send OTP again and verify it |
| `HPR-010` Communication Mobile Number verification-I | mandatory | none | none | `/api/v1/registration/aadhaar/demographicAuthViaMobile`: no operation in the specifications has this path; near: m4_post_v2_registration_aadhaar_demographicauthviamobile (POST /v2/registration/aadhaar/demographicAuthViaMobile) | Not stated. |
| `HPR-011` Communication Mobile Number verification-II | mandatory | none | none | `/api/v1/registration/aadhaar/createHprIdWithPreVerified`: no operation in the specifications has this path; near: m4_post_v2_registration_aadhaar_createhpridwithpreverified (POST /v2/registration/aadhaar/createHprIdWithPreVerified) | Not stated. |
| `No id` Aadhaar collection and Error Message | no case id in NHA's sheet, so the workbook and the report cannot record it | none | none | `/api/v1/registration/aadhaar/generateOtp`: no operation in the specifications has this path; near: m4_post_v1_registration_aadhaar_generatemobileotp (POST /v1/registration/aadhaar/generateMobileOTP) | User is prompted to enter correct Aadhaar Number |

## HPR: Register In HPR

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HPR-018` hprToken | mandatory | `m4_post_v1_doctors_register_professional_new` | `m4-enrollment` | none | Received Token |
| `HPR-020` Healthcare Professional ID | mandatory | none | none | none | Healthcare Professional ID is successfully genearted |
| `HPR-026` Salutation * | mandatory | none | none | none | Not stated. |
| `HPR-027` First Name * | mandatory | none | none | none | First name is fetched from the Aadhaar database. |
| `HPR-028` Middle Name | mandatory | none | none | none | Middle Name is fetched from the Aadhaar database. |
| `HPR-029` Last Name | mandatory | none | none | none | Last name is fetched from the Aadhaar database. |
| `HPR-033` Nationality | mandatory | none | none | none | Not stated. |
| `HPR-036` Languages spoken * | mandatory | none | none | none | Not stated. |
| `HPR-037` Address as per KYC : | mandatory | none | none | none | Not stated. |
| `HPR-054` category | mandatory | none | none | none | Not stated. |
| `HPR-055` categoryId | mandatory | none | none | none | Not stated. |
| `HPR-056` registeredWithCouncil | mandatory | none | none | none | Not stated. |
| `HPR-057` registrationNumber | mandatory | none | none | none | Not stated. |
| `HPR-058` registrationDate | mandatory | none | none | none | Not stated. |
| `HPR-059` registrationCertificate | mandatory | none | none | none | Not stated. |
| `HPR-062` nameOfDegreeOrDiplomaObtained | mandatory | none | none | none | Not stated. |
| `HPR-063` country | mandatory | none | none | none | Not stated. |
| `HPR-064` state | mandatory | none | none | none | Not stated. |
| `HPR-065` college | mandatory | none | none | none | Not stated. |
| `HPR-066` university | mandatory | none | none | none | Not stated. |
| `HPR-068` yearOfAwardingDegreeDiploma | mandatory | none | none | none | Not stated. |
| `HPR-069` degreeCertificate | mandatory | none | none | none | Not stated. |
| `HPR-070` isNameDifferentInCertificate | mandatory | none | none | none | Not stated. |
| `HPR-072` Are you currentlty working (Yes/No) | mandatory | none | none | none | Not stated. |
| `HPR-073` IF No, so please ask the reason for non working | mandatory | none | none | none | Not stated. |
| `HPR-074` If Yes, So radio button (Govt, Pvt or Both) | mandatory | none | none | none | Not stated. |
| `HPR-075` If Govt /Both, so document attachment is mandatory to upload upto 5 mb | mandatory | none | none | none | Not stated. |
| `HPR-076` If Govt/Both so facility decalartion is mandatory (Professinal can search the facility via name/Facility id) | mandatory | `m4_post_facilitymanagement_v1_5_facility_search` | `m4-search` | none | Not stated. |
| `HPR-077` Preview Profile & Submit | mandatory | `m4_post_v1_doctors_register_professional_new` | `m4-enrollment` | none | Not stated. |
| `HPR-019` profilePhoto | optional | none | none | none | Photo will be displayed |
| `HPR-022` Mobile Number | optional | none | none | none | Not stated. |
| `HPR-024` Email and Email Status | optional | none | none | none | Not stated. |
| `HPR-030` Father's Name | optional | none | none | none | User is able to fill the Father's Name. |
| `HPR-031` Mother's Name | optional | none | none | none | User is able to fill the Mother's Name. |
| `HPR-032` Spouse's Name | optional | none | none | none | User is able to fill the Spouse Name. |
| `HPR-038` Is this communication address same as your address as per KYC ? | optional | none | none | none | Not stated. |
| `HPR-039` Communication Address * | optional | none | none | none | Not stated. |
| `HPR-040` Name * | optional | none | none | none | Not stated. |
| `HPR-041` Country* | optional | none | none | none | Not stated. |
| `HPR-042` State/Union Territory * | optional | none | none | none | Not stated. |
| `HPR-043` District * | optional | none | none | none | Not stated. |
| `HPR-044` Sub District | optional | none | none | none | Not stated. |
| `HPR-045` City/Town/Village | optional | none | none | none | Not stated. |
| `HPR-046` Postal Code(PIN) * | optional | none | none | none | Not stated. |
| `HPR-060` isNameDifferentInCertificate | optional | none | none | none | Not stated. |
| `HPR-061` proofOfNameChangeCertificate | optional | none | none | none | Not stated. |
| `HPR-067` monthOfAwardingDegreeDiploma | optional | none | none | none | Not stated. |
| `HPR-071` proofOfNameChangeCertificate | optional | none | none | none | Not stated. |

## HPR: Fetch Professionals details

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HPR-078` fetchProfessionalsdetails | optional | `m4_post_v1_doctors_fetch_professional_info` | `m4-enrollment` | none | Not stated. |

## HPR: Update Professionals details

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HPR-079` update-professional | mandatory | none | none | `/apis/v1/doctors/update-professional`: no operation in the specifications has this path; near: m4_post_v1_doctors_update_professional_new (POST /apis/v1/doctors/update-professional-new) | Not stated. |

## HPR: Upload Document API

| Case | Marking | Operations the sheet lists for this group | Journeys | Unresolved | Expected Result |
| --- | --- | --- | --- | --- | --- |
| `HPR-080` Upload- Document | optional | `m4_post_v1_doctors_fetch_documents_list`, `m4_post_v1_uploads_upload_document` | `m4-enrollment` | none | Not stated. |
