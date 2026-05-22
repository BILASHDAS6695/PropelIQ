# Functional Requirements Specification

## Document Information

| Field | Value |
|-------|-------|
| **Project** | Unified Patient Access & Clinical Intelligence Platform |
| **Version** | 1.0 |
| **Status** | Draft |
| **Source** | BRD.md |
| **Phase** | Phase 1 |

---

## 1. Introduction

### 1.1 Purpose

This document defines the functional requirements and use case specifications for the Unified Patient Access & Clinical Intelligence Platform. It translates business needs from the BRD into testable, implementable requirements with full traceability.

### 1.2 Scope

The platform combines patient-centric appointment scheduling with a clinical intelligence engine that extracts structured data from unstructured clinical documents. Phase 1 covers patient booking, staff administration, clinical data aggregation, and medical coding—all deployed on free/open-source infrastructure.

### 1.3 Definitions and Acronyms

| Term | Definition |
|------|-----------|
| FR | Functional Requirement |
| UC | Use Case |
| NFR | Non-Functional Requirement |
| ICD-10 | International Classification of Diseases, 10th Revision |
| CPT | Current Procedural Terminology |
| HIPAA | Health Insurance Portability and Accountability Act |
| RBAC | Role-Based Access Control |

---

## 2. Stakeholders and Actors

| Actor | Description |
|-------|-------------|
| **Patient** | End-user who books appointments, completes intake, and uploads clinical documents |
| **Staff** | Front desk or call center personnel managing walk-ins, same-day queues, and patient arrivals |
| **Admin** | System administrator responsible for user management and configuration |
| **System** | Automated platform processes (reminders, slot swap, data extraction) |

---

## 3. Functional Requirements

### 3.1 Authentication and Access Control

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-001 | The system shall allow patients to self-register with email, name, phone, and password. | High | §6, §7 |
| FR-002 | The system shall allow Admin users to create Staff and Admin accounts. | High | §6 |
| FR-003 | The system shall enforce role-based access control for Patient, Staff, and Admin roles. | High | §7 |
| FR-004 | The system shall automatically terminate inactive sessions after 15 minutes. | High | §7 |
| FR-005 | The system shall support secure authentication with encrypted credential storage. | High | §7 |

### 3.2 Appointment Booking

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-006 | The system shall allow patients to search for available appointment slots by provider, date, and time. | High | §4, §6 |
| FR-007 | The system shall allow patients to book an available appointment slot. | High | §4, §6 |
| FR-008 | The system shall allow staff to book appointments on behalf of patients. | High | §4 |
| FR-009 | The system shall allow staff to create walk-in bookings, optionally creating a patient account during the process. | High | §4 |
| FR-010 | The system shall allow staff to manage a same-day appointment queue. | High | §4 |
| FR-011 | The system shall allow staff to mark a patient as "Arrived". | High | §4 |
| FR-012 | The system shall prevent patients from self-check-in via app, web portal, or QR code. | High | §4, §6 |
| FR-013 | The system shall generate and email a PDF containing appointment details upon successful booking. | Medium | §6 |

### 3.3 Preferred Slot Swap (Waitlist)

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-014 | The system shall allow patients to select a preferred unavailable slot while booking an available slot. | High | §4 |
| FR-015 | The system shall monitor preferred slot availability and automatically swap the appointment when the preferred slot opens. | High | §4 |
| FR-016 | The system shall release the original booked slot upon successful swap to the preferred slot. | High | §4 |
| FR-017 | The system shall notify the patient when a preferred slot swap is executed. | Medium | §4 |

### 3.4 Reminders and Notifications

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-018 | The system shall send automated appointment reminders via SMS. | High | §6 |
| FR-019 | The system shall send automated appointment reminders via email. | High | §6 |
| FR-020 | The system shall perform rule-based no-show risk assessment for booked appointments. | Medium | §2, §4 |
| FR-021 | The system shall support configurable reminder timing (e.g., 24h, 2h before appointment). | Medium | §6 |

### 3.5 Calendar Integration

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-022 | The system shall sync booked appointments to Google Calendar via free APIs. | Medium | §6 |
| FR-023 | The system shall sync booked appointments to Outlook Calendar via free APIs. | Medium | §6 |
| FR-024 | The system shall update external calendar entries when appointments are modified or cancelled. | Medium | §6 |

### 3.6 Insurance Pre-Check

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-025 | The system shall perform soft validation of insurance name and ID against an internal predefined set of dummy records. | Medium | §6 |
| FR-026 | The system shall display validation results (valid/invalid/not found) to the user. | Medium | §6 |

### 3.7 Patient Intake

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-027 | The system shall provide an AI-assisted conversational intake mode for collecting patient information. | High | §4 |
| FR-028 | The system shall provide a traditional manual form intake mode as an alternative. | High | §4 |
| FR-029 | The system shall allow patients to switch between AI conversational and manual form intake modes at any point during the process. | High | §4 |
| FR-030 | The system shall allow patients to edit submitted intake data without requiring staff assistance. | High | §4 |
| FR-031 | The system shall persist intake data regardless of which mode was used for collection. | High | §4 |

### 3.8 Clinical Document Management

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-032 | The system shall allow patients to upload historical clinical documents in PDF format. | High | §3, §6 |
| FR-033 | The system shall ingest post-visit clinical notes for processing. | High | §3 |
| FR-034 | The system shall extract structured data (vitals, medical history, medications) from uploaded unstructured documents. | High | §3, §4 |
| FR-035 | The system shall aggregate data from multiple uploaded documents into a single patient profile. | High | §4 |

### 3.9 360-Degree Patient View

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-036 | The system shall generate a unified, verified 360-Degree Patient View from aggregated clinical data. | High | §3, §4 |
| FR-037 | The system shall de-duplicate data entries across multiple source documents. | High | §4 |
| FR-038 | The system shall explicitly highlight critical data conflicts (e.g., conflicting medications from different sources). | High | §4 |
| FR-039 | The system shall provide a conflict resolution interface for staff to review and resolve identified conflicts. | High | §4 |
| FR-040 | The system shall transform the 20-minute manual search task into a 2-minute verification action. | High | §3 |

### 3.10 Medical Coding

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-041 | The system shall map extracted clinical data to ICD-10 codes. | High | §6 |
| FR-042 | The system shall map extracted clinical data to CPT codes. | High | §6 |
| FR-043 | The system shall present suggested codes with confidence indicators for staff verification. | High | §3, §8 |
| FR-044 | The system shall achieve an AI-Human Agreement Rate of greater than 98% for suggested codes. | High | §8 |

### 3.11 Audit and Compliance

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-045 | The system shall maintain immutable audit logs for all patient data access and modifications. | High | §7 |
| FR-046 | The system shall maintain immutable audit logs for all staff actions. | High | §7 |
| FR-047 | The system shall ensure HIPAA-compliant data handling, transmission, and storage. | High | §7 |
| FR-048 | The system shall encrypt all data in transit and at rest. | High | §7 |

### 3.12 Administration

| ID | Requirement | Priority | BRD Ref |
|----|-------------|----------|---------|
| FR-049 | The system shall allow Admin users to create, update, and deactivate user accounts. | High | §6 |
| FR-050 | The system shall allow Admin users to assign and modify user roles. | High | §6 |
| FR-051 | The system shall provide Admin access to audit logs and system reports. | Medium | §7 |

---

## 4. Use Cases

### UC-001: Patient Books Appointment

| Field | Value |
|-------|-------|
| **ID** | UC-001 |
| **Title** | Patient Books Appointment |
| **Actor** | Patient |
| **Preconditions** | Patient is authenticated; available appointment slots exist |
| **Trigger** | Patient navigates to the booking interface |

**Main Flow:**

1. Patient selects a provider and desired date.
2. System displays available appointment slots.
3. Patient selects an available slot.
4. System prompts for optional preferred slot selection (see UC-004).
5. Patient confirms the booking.
6. System creates the appointment record.
7. System generates a confirmation PDF and sends it via email.
8. System syncs the appointment to the patient's external calendar (if configured).

**Postconditions:** Appointment is booked; confirmation sent; calendar updated.

**Alternative Flows:**

- **4a.** Patient selects a preferred unavailable slot → system registers the preference (triggers UC-004 monitoring).
- **5a.** No slots available → system displays waitlist option.

**Related FRs:** FR-006, FR-007, FR-013, FR-014, FR-022, FR-023

---

### UC-002: Staff Books Appointment for Patient

| Field | Value |
|-------|-------|
| **ID** | UC-002 |
| **Title** | Staff Books Appointment for Patient |
| **Actor** | Staff |
| **Preconditions** | Staff is authenticated; patient record exists or can be created |
| **Trigger** | Staff initiates booking via patient call or in-person request |

**Main Flow:**

1. Staff searches for the patient record.
2. Staff selects provider, date, and available slot.
3. Staff confirms the booking on behalf of the patient.
4. System creates the appointment record.
5. System sends confirmation PDF to patient's email.
6. System sends appointment reminder according to configured timing.

**Postconditions:** Appointment is booked under the patient's account.

**Alternative Flows:**

- **1a.** Patient not found → Staff creates a new patient record, then proceeds.

**Related FRs:** FR-008, FR-013, FR-018, FR-019

---

### UC-003: Staff Registers Walk-in Patient

| Field | Value |
|-------|-------|
| **ID** | UC-003 |
| **Title** | Staff Registers Walk-in Patient |
| **Actor** | Staff |
| **Preconditions** | Staff is authenticated |
| **Trigger** | Patient arrives without a prior appointment |

**Main Flow:**

1. Staff selects "Walk-in" booking option.
2. Staff searches for existing patient record.
3. If patient exists, Staff selects the record.
4. If patient does not exist, Staff optionally creates a new account with basic demographics.
5. Staff assigns the patient to the same-day queue.
6. System creates the walk-in appointment record with timestamp.
7. Staff marks patient as "Arrived".

**Postconditions:** Walk-in is registered; patient is in the same-day queue; arrival is recorded.

**Alternative Flows:**

- **4a.** Staff chooses not to create an account → system creates a one-time visit record.

**Related FRs:** FR-009, FR-010, FR-011

---

### UC-004: Preferred Slot Swap Execution

| Field | Value |
|-------|-------|
| **ID** | UC-004 |
| **Title** | Preferred Slot Swap Execution |
| **Actor** | System |
| **Preconditions** | Patient has a confirmed booking with a registered preferred slot preference |
| **Trigger** | The preferred slot becomes available (cancellation or schedule change) |

**Main Flow:**

1. System detects that a preferred slot has become available.
2. System validates the patient's existing booking is still active.
3. System moves the appointment to the preferred slot.
4. System releases the original slot back to the available pool.
5. System notifies the patient of the swap via SMS and email.
6. System updates external calendar entry (if configured).

**Postconditions:** Appointment is at the preferred time; original slot is available for others.

**Alternative Flows:**

- **2a.** Patient's booking was already cancelled → system discards the preference.
- **3a.** Multiple patients prefer the same slot → system awards to the earliest registrant.

**Related FRs:** FR-014, FR-015, FR-016, FR-017

---

### UC-005: Patient Completes AI Conversational Intake

| Field | Value |
|-------|-------|
| **ID** | UC-005 |
| **Title** | Patient Completes AI Conversational Intake |
| **Actor** | Patient |
| **Preconditions** | Patient is authenticated; appointment is booked |
| **Trigger** | Patient accesses the intake section |

**Main Flow:**

1. System presents the AI conversational intake interface.
2. AI asks structured questions about medical history, current symptoms, medications, and allergies.
3. Patient responds to each question conversationally.
4. AI parses responses and populates structured intake fields.
5. System displays a summary of captured data for patient review.
6. Patient confirms or edits the intake data.
7. System persists the finalized intake record.

**Postconditions:** Intake data is saved to the patient's profile.

**Alternative Flows:**

- **1a.** Patient switches to manual form mode → continue at UC-006 step 2.
- **6a.** Patient edits data → system updates fields and re-displays summary.

**Related FRs:** FR-027, FR-029, FR-030, FR-031

---

### UC-006: Patient Completes Manual Form Intake

| Field | Value |
|-------|-------|
| **ID** | UC-006 |
| **Title** | Patient Completes Manual Form Intake |
| **Actor** | Patient |
| **Preconditions** | Patient is authenticated; appointment is booked |
| **Trigger** | Patient selects manual form intake or switches from AI mode |

**Main Flow:**

1. System presents the structured intake form with fields for demographics, medical history, medications, allergies, and current symptoms.
2. Patient fills in the required fields.
3. Patient submits the form.
4. System validates required fields.
5. System persists the intake record.

**Postconditions:** Intake data is saved to the patient's profile.

**Alternative Flows:**

- **1a.** Patient switches to AI conversational mode → continue at UC-005 step 2.
- **4a.** Validation fails → system highlights missing/invalid fields for correction.

**Related FRs:** FR-028, FR-029, FR-030, FR-031

---

### UC-007: Patient Uploads Clinical Document

| Field | Value |
|-------|-------|
| **ID** | UC-007 |
| **Title** | Patient Uploads Clinical Document |
| **Actor** | Patient |
| **Preconditions** | Patient is authenticated |
| **Trigger** | Patient navigates to document upload section |

**Main Flow:**

1. Patient selects one or more PDF files for upload.
2. System validates file format (PDF) and size constraints.
3. System stores the document securely with HIPAA-compliant encryption.
4. System queues the document for data extraction processing.
5. System confirms successful upload to the patient.
6. System extracts structured data (vitals, history, medications) from the document.
7. System integrates extracted data into the patient's aggregated profile.

**Postconditions:** Document is stored; extracted data is available in the 360-Degree Patient View.

**Alternative Flows:**

- **2a.** Invalid file format → system rejects upload with error message.
- **6a.** Extraction encounters ambiguous data → system flags for staff review.

**Related FRs:** FR-032, FR-034, FR-035

---

### UC-008: System Generates 360-Degree Patient View

| Field | Value |
|-------|-------|
| **ID** | UC-008 |
| **Title** | System Generates 360-Degree Patient View |
| **Actor** | System |
| **Preconditions** | At least one clinical document has been processed for the patient |
| **Trigger** | New document processed or staff requests patient view |

**Main Flow:**

1. System retrieves all extracted data records for the patient.
2. System performs de-duplication across data sources.
3. System identifies conflicting data points (e.g., different medication lists from different documents).
4. System constructs the unified patient view with consolidated data.
5. System highlights all identified conflicts with source attribution.
6. System makes the view available to authorized staff.

**Postconditions:** Unified patient view is current and reflects all processed documents.

**Alternative Flows:**

- **3a.** No conflicts detected → system presents clean unified view.
- **5a.** Critical conflict detected (e.g., contradicting medications) → system generates a priority alert.

**Related FRs:** FR-036, FR-037, FR-038, FR-040

---

### UC-009: Staff Resolves Data Conflict

| Field | Value |
|-------|-------|
| **ID** | UC-009 |
| **Title** | Staff Resolves Data Conflict |
| **Actor** | Staff |
| **Preconditions** | 360-Degree Patient View contains highlighted conflicts |
| **Trigger** | Staff opens patient view with unresolved conflicts |

**Main Flow:**

1. System displays the 360-Degree Patient View with conflicts highlighted.
2. Staff selects a conflict to resolve.
3. System displays conflicting values with source document references.
4. Staff selects the correct value or enters a reconciled value.
5. System updates the patient profile with the resolved data.
6. System logs the resolution action in the audit trail.

**Postconditions:** Conflict is resolved; audit trail updated; patient view reflects the resolution.

**Related FRs:** FR-038, FR-039, FR-045, FR-046

---

### UC-010: System Performs Insurance Pre-Check

| Field | Value |
|-------|-------|
| **ID** | UC-010 |
| **Title** | System Performs Insurance Pre-Check |
| **Actor** | System |
| **Preconditions** | Patient has provided insurance name and ID |
| **Trigger** | Patient submits insurance information during booking or intake |

**Main Flow:**

1. Patient enters insurance provider name and member ID.
2. System queries the internal predefined set of dummy insurance records.
3. System matches insurance name and ID against available records.
4. System returns validation result: Valid, Invalid, or Not Found.
5. System displays the result to the user.

**Postconditions:** Insurance validation status is recorded on the patient's profile.

**Alternative Flows:**

- **3a.** No match found → system displays "Not Found" with instructions to contact staff.
- **4a.** Partial match (name matches, ID does not) → system displays "Invalid" with guidance.

**Related FRs:** FR-025, FR-026

---

### UC-011: System Sends Appointment Reminders

| Field | Value |
|-------|-------|
| **ID** | UC-011 |
| **Title** | System Sends Appointment Reminders |
| **Actor** | System |
| **Preconditions** | Appointment is confirmed; reminder schedule is configured |
| **Trigger** | Scheduled reminder time is reached (e.g., 24h or 2h before appointment) |

**Main Flow:**

1. System identifies appointments approaching their reminder window.
2. System evaluates the no-show risk score for each appointment.
3. System sends SMS reminder to the patient's registered phone.
4. System sends email reminder to the patient's registered email.
5. System logs the reminder delivery status.

**Postconditions:** Reminders sent; delivery status recorded.

**Alternative Flows:**

- **2a.** High no-show risk detected → system sends additional reminder or escalates to staff.
- **3a.** SMS delivery fails → system logs failure and retries once.

**Related FRs:** FR-018, FR-019, FR-020, FR-021

---

### UC-012: Admin Manages User Accounts

| Field | Value |
|-------|-------|
| **ID** | UC-012 |
| **Title** | Admin Manages User Accounts |
| **Actor** | Admin |
| **Preconditions** | Admin is authenticated |
| **Trigger** | Admin navigates to user management |

**Main Flow:**

1. Admin searches for an existing user or selects "Create New User".
2. For new users: Admin enters user details (name, email, role).
3. System creates the account and sends activation email.
4. For existing users: Admin updates profile fields or role assignment.
5. System saves changes and logs the action in the audit trail.

**Postconditions:** User account is created/updated; audit log reflects the change.

**Alternative Flows:**

- **4a.** Admin deactivates a user → system revokes access immediately and logs the action.
- **2a.** Email already exists → system displays error and prevents duplicate.

**Related FRs:** FR-049, FR-050, FR-051

---

### UC-013: System Performs Medical Coding

| Field | Value |
|-------|-------|
| **ID** | UC-013 |
| **Title** | System Performs Medical Coding |
| **Actor** | System |
| **Preconditions** | 360-Degree Patient View data is available |
| **Trigger** | Patient view is generated or updated |

**Main Flow:**

1. System analyzes aggregated clinical data from the patient view.
2. System maps diagnoses and conditions to ICD-10 codes.
3. System maps procedures and services to CPT codes.
4. System assigns confidence scores to each suggested code.
5. System presents suggested codes with confidence indicators to staff.
6. Staff reviews and confirms or adjusts the codes.

**Postconditions:** Medical codes are assigned to the patient record; staff verification is logged.

**Alternative Flows:**

- **4a.** Confidence below threshold → system flags code for mandatory review.
- **6a.** Staff rejects a code → system logs the override and removes the code.

**Related FRs:** FR-041, FR-042, FR-043, FR-044

---

### UC-014: Staff Marks Patient as Arrived

| Field | Value |
|-------|-------|
| **ID** | UC-014 |
| **Title** | Staff Marks Patient as Arrived |
| **Actor** | Staff |
| **Preconditions** | Patient has a booked appointment for today |
| **Trigger** | Patient physically arrives at the facility |

**Main Flow:**

1. Staff searches for the patient's appointment in the daily schedule.
2. Staff selects the appointment record.
3. Staff marks the patient as "Arrived".
4. System records the arrival timestamp.
5. System updates the patient's status in the same-day queue.

**Postconditions:** Patient status is "Arrived"; queue is updated.

**Related FRs:** FR-011

---

### UC-015: System Syncs with External Calendar

| Field | Value |
|-------|-------|
| **ID** | UC-015 |
| **Title** | System Syncs with External Calendar |
| **Actor** | System |
| **Preconditions** | Patient has connected a Google or Outlook calendar |
| **Trigger** | Appointment is created, modified, or cancelled |

**Main Flow:**

1. System detects an appointment state change.
2. System identifies the patient's connected calendar service.
3. System pushes the event create/update/delete to the external calendar API.
4. System confirms sync success.
5. System logs the sync action.

**Postconditions:** External calendar reflects the current appointment state.

**Alternative Flows:**

- **4a.** Sync fails → system retries with exponential backoff; logs failure after max retries.

**Related FRs:** FR-022, FR-023, FR-024

---

## 5. Non-Functional Requirements

| ID | Category | Requirement | BRD Ref |
|----|----------|-------------|---------|
| NFR-001 | Security | All data transmission shall use TLS 1.2+ encryption. | §7 |
| NFR-002 | Security | All stored patient data shall be encrypted at rest using AES-256. | §7 |
| NFR-003 | Compliance | The system shall be 100% HIPAA-compliant in data handling, transmission, and storage. | §7 |
| NFR-004 | Availability | The system shall target 99.9% uptime. | §7 |
| NFR-005 | Performance | The 360-Degree Patient View shall be generated within 2 minutes of data availability. | §3 |
| NFR-006 | Session | Inactive sessions shall timeout after 15 minutes. | §7 |
| NFR-007 | Infrastructure | The system shall support native deployment on Windows Services/IIS. | §7 |
| NFR-008 | Infrastructure | The system shall use PostgreSQL for structured data storage. | §5 |
| NFR-009 | Infrastructure | The system shall use Upstash Redis for caching. | §7 |
| NFR-010 | Infrastructure | The system shall be hosted on free/open-source-friendly platforms (Netlify, Vercel, GitHub Codespaces). | §5 |
| NFR-011 | Technology | The frontend shall be built with Angular. | §5 |
| NFR-012 | Technology | The backend API shall be built with .NET. | §5 |
| NFR-013 | Scalability | Auxiliary processing shall use strictly free and open-source tools. | §5 |

---

## 6. Assumptions and Constraints

### 6.1 Assumptions

1. Patients have access to email and/or SMS-capable phones for receiving notifications.
2. Clinical documents uploaded by patients are in PDF format.
3. The internal insurance record set is pre-populated with dummy data for Phase 1 validation.
4. Free-tier API limits for Google/Outlook calendar sync are sufficient for Phase 1 volume.
5. AI extraction models are available and trained for clinical document parsing.

### 6.2 Constraints

1. No paid cloud hosting (AWS, Azure) is permitted in Phase 1.
2. No provider-facing roles or actions are in scope.
3. No payment gateway integration.
4. No patient self-check-in capabilities.
5. No direct bi-directional EHR integration.
6. No family member profile features.
7. No full claims submission.

---

## 7. Traceability Matrix

| BRD Section | Functional Requirements | Use Cases |
|-------------|------------------------|-----------|
| §2 (Business Problem) | FR-020 | UC-011 |
| §3 (Proposed Solution) | FR-034, FR-036, FR-040, FR-041, FR-042, FR-043 | UC-007, UC-008, UC-013 |
| §4 (Core Features) | FR-006–FR-012, FR-014–FR-017, FR-027–FR-031, FR-035–FR-039 | UC-001–UC-009 |
| §5 (Technology Stack) | NFR-008–NFR-013 | — |
| §6 (Phase 1 Scope) | FR-006–FR-009, FR-013, FR-018–FR-026, FR-032, FR-033, FR-041, FR-042, FR-049–FR-051 | UC-001–UC-003, UC-010–UC-012, UC-015 |
| §7 (NFRs) | FR-004, FR-005, FR-045–FR-048 | — |
| §8 (Success Criteria) | FR-020, FR-040, FR-044, FR-038 | UC-011, UC-008, UC-013, UC-009 |

---

## 8. Out-of-Scope Confirmation

The following items are explicitly excluded from Phase 1 per BRD §6:

- Provider logins or provider-facing actions.
- Payment gateway integration.
- Family member profile features.
- Patient self-check-in (mobile, web, QR).
- Direct bi-directional EHR integration.
- Full claims submission.
- Paid cloud infrastructure usage.

---

## 9. Open Questions

| # | Question | Impact Area |
|---|----------|-------------|
| 1 | What specific no-show risk rules will be implemented (history-based, demographic, time-of-day)? | FR-020 |
| 2 | What is the maximum file size for clinical document uploads? | FR-032 |
| 3 | How many preferred slot swap preferences can a patient hold simultaneously? | FR-014 |
| 4 | What is the conflict prioritization logic when multiple patients prefer the same slot? | UC-004 |
| 5 | What specific data fields constitute the minimum viable 360-Degree Patient View? | FR-036 |
| 6 | What are the specific free calendar APIs to be used (Google Calendar API free tier, Microsoft Graph)? | FR-022, FR-023 |
| 7 | What SMS provider will be used for free-tier reminder delivery? | FR-018 |
| 8 | What AI/NLP model will power the conversational intake and document extraction? | FR-027, FR-034 |
