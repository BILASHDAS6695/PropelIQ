# Master Test Plan
## Unified Patient Access & Clinical Intelligence Platform

## Document Information

| Field | Value |
|-------|-------|
| **Project** | Unified Patient Access & Clinical Intelligence Platform |
| **Document Type** | Master Test Plan |
| **Version** | 1.0 |
| **Created Date** | 2026-06-10 |
| **Status** | Draft |
| **Source Document** | spec.md v1.0 |
| **Test Scope** | Phase 1 - Full System |

---

## 1. Executive Summary

This Master Test Plan defines the comprehensive testing strategy for the Unified Patient Access & Clinical Intelligence Platform Phase 1. The plan ensures complete coverage of:

- **51 Functional Requirements** (FR-001 through FR-051)
- **15 Use Cases** (UC-001 through UC-015)
- **13 Non-Functional Requirements** (NFR-001 through NFR-013)

The testing approach spans multiple levels (unit, integration, system, acceptance) across three technology stacks (.NET API, Angular frontend, Python AI service) and validates end-to-end workflows including appointment booking, AI-assisted patient intake, clinical document processing, and medical coding.

**Primary Quality Gates**:
- 100% FR coverage with traceable test cases
- 98%+ AI-Human Agreement Rate for medical coding (FR-044)
- Sub-2-minute 360-Degree Patient View generation (NFR-005, FR-040)
- HIPAA compliance validation (NFR-003, FR-047)
- 99.9% system uptime (NFR-004)

---

## 2. Test Objectives

### 2.1 Primary Objectives

1. **Validate Functional Completeness**: Verify all 51 functional requirements are implemented correctly
2. **Ensure Use Case Coverage**: Test all 15 use cases with positive, negative, and edge case scenarios
3. **Verify Non-Functional Compliance**: Validate performance, security, compliance, and scalability requirements
4. **Establish Traceability**: Map every test case back to FR/UC/NFR requirements
5. **Validate Cross-Stack Integration**: Ensure seamless interaction between .NET API, Angular UI, and Python AI service

### 2.2 Quality Criteria

| Metric | Target | Requirement Ref |
|--------|--------|-----------------|
| FR Test Coverage | 100% | All FRs |
| Code Coverage (Unit Tests) | ≥80% | Development Standard |
| AI-Human Agreement (Medical Coding) | >98% | FR-044 |
| 360-Degree View Generation Time | <2 minutes | NFR-005, FR-040 |
| Session Timeout | 15 minutes | NFR-006, FR-004 |
| System Uptime | 99.9% | NFR-004 |
| Critical/High Defect Leakage | 0% | Quality Gate |

---

## 3. Test Strategy

### 3.1 Test Levels

| Level | Scope | Tools | Responsibility |
|-------|-------|-------|----------------|
| **Unit** | Individual components, services, handlers | xUnit (.NET), Jasmine/Karma (Angular), pytest (Python) | Development Team |
| **Integration** | API endpoints, database interactions, service integrations | xUnit, Postman, pytest | Development Team |
| **System** | End-to-end workflows, cross-stack scenarios | Playwright, Selenium, Postman Collections | QA Team |
| **Acceptance** | Business use case validation | Manual UAT, Playwright | Product Owner + QA |
| **Performance** | Load, stress, scalability | k6, Apache JMeter | DevOps + QA |
| **Security** | OWASP Top 10, HIPAA compliance | OWASP ZAP, SonarQube, penetration testing | Security Team + QA |

### 3.2 Test Approach

#### 3.2.1 Functional Testing
- **Black-box testing** for all API endpoints
- **Behavior-driven** tests for use cases
- **Boundary value analysis** for input validation
- **Equivalence partitioning** for test data optimization

#### 3.2.2 Non-Functional Testing
- **Performance testing**: Load tests with 100, 500, 1000 concurrent users
- **Security testing**: OWASP ZAP automated scans, manual penetration testing
- **Compliance testing**: HIPAA audit log validation, encryption verification
- **Usability testing**: Manual UAT sessions with 5+ end users per role

#### 3.2.3 AI/ML Testing
- **Ground truth validation**: Medical coding against expert-annotated dataset
- **Confidence threshold tuning**: FR-043 confidence indicators
- **Extraction accuracy**: Clinical data extraction precision/recall metrics
- **Regression testing**: Model version change impact analysis

### 3.3 Test Environment

| Environment | Purpose | Configuration |
|-------------|---------|---------------|
| **Local Development** | Unit & integration tests | In-memory DB, mock services |
| **CI/CD Pipeline** | Automated regression | Docker containers, PostgreSQL test DB |
| **QA Environment** | System & acceptance testing | Full stack deployed (Vercel/Netlify frontend, .NET API, PostgreSQL, Upstash Redis) |
| **Staging** | Pre-production validation | Production-mirror configuration |

---

## 4. Functional Requirements Test Coverage

### 4.1 Authentication and Access Control (FR-001 to FR-005)

#### TC-AUTH-001: Patient Self-Registration (FR-001)
**Priority**: High  
**Use Case**: UC-001 (precondition)  
**Test Steps**:
1. Navigate to registration page
2. Enter email, name, phone, password (meets complexity requirements)
3. Submit registration form
4. Verify account created in database
5. Verify activation email sent
6. Verify user can authenticate with credentials

**Expected Result**: Patient account created; activation email delivered; login successful  
**Test Data**: Valid email, 10+ char password with uppercase/lowercase/digit/special char  
**Negative Tests**: Duplicate email, weak password, invalid email format, missing required fields

---

#### TC-AUTH-002: Admin Creates Staff/Admin Accounts (FR-002)
**Priority**: High  
**Use Case**: UC-012  
**Test Steps**:
1. Authenticate as Admin
2. Navigate to user management
3. Create new Staff user with email, name, role='Staff'
4. Create new Admin user with role='Admin'
5. Verify accounts created with correct roles
6. Verify activation emails sent

**Expected Result**: Staff and Admin accounts created with proper role assignments  
**Negative Tests**: Non-admin attempts to create user, duplicate email, invalid role

---

#### TC-AUTH-003: Role-Based Access Control Enforcement (FR-003)
**Priority**: High  
**Test Steps**:
1. As Patient: Attempt to access admin endpoints → expect 403 Forbidden
2. As Patient: Attempt to access staff-only endpoints → expect 403
3. As Staff: Attempt to access admin-only endpoints → expect 403
4. As Staff: Access staff-allowed endpoints → expect 200 OK
5. As Admin: Access all endpoints → expect 200 OK

**Expected Result**: Authorization enforced per role; unauthorized access denied  
**Traceability**: FR-003

---

#### TC-AUTH-004: Session Timeout After 15 Minutes (FR-004, NFR-006)
**Priority**: High  
**Test Steps**:
1. Authenticate as any user
2. Perform action → verify success
3. Wait 14 minutes → perform action → verify success
4. Wait 16 minutes total → perform action → expect 401 Unauthorized
5. Re-authenticate → verify success

**Expected Result**: Session expires after 15 minutes of inactivity  
**Traceability**: FR-004, NFR-006

---

#### TC-AUTH-005: Secure Credential Storage (FR-005)
**Priority**: High  
**Test Steps**:
1. Register a patient with password "TestPass123!"
2. Query database directly for user record
3. Verify password field contains bcrypt hash (not plaintext)
4. Verify hash starts with "$2a$" or "$2b$" (bcrypt identifier)
5. Attempt authentication with correct password → expect success
6. Attempt authentication with wrong password → expect failure

**Expected Result**: Passwords stored as bcrypt hashes; never in plaintext  
**Traceability**: FR-005

---

### 4.2 Appointment Booking (FR-006 to FR-013)

#### TC-APPT-001: Patient Searches Available Slots (FR-006)
**Priority**: High  
**Use Case**: UC-001  
**Test Steps**:
1. Authenticate as Patient
2. Navigate to booking interface
3. Select provider "Dr. Smith"
4. Select date "2026-06-15"
5. Verify system displays available time slots (e.g., 9:00 AM, 10:00 AM, 2:00 PM)
6. Verify unavailable slots are not shown or marked as unavailable

**Expected Result**: Available slots displayed; unavailable slots excluded  
**Test Data**: Pre-seed database with provider availability

---

#### TC-APPT-002: Patient Books Available Slot (FR-007, FR-013)
**Priority**: High  
**Use Case**: UC-001  
**Test Steps**:
1. Complete TC-APPT-001
2. Select available slot "10:00 AM"
3. Confirm booking
4. Verify appointment record created in database
5. Verify confirmation PDF generated
6. Verify confirmation email sent to patient
7. Verify slot no longer available for other patients

**Expected Result**: Appointment booked; confirmation sent; slot reserved  
**Traceability**: FR-007, FR-013

---

#### TC-APPT-003: Staff Books Appointment for Patient (FR-008)
**Priority**: High  
**Use Case**: UC-002  
**Test Steps**:
1. Authenticate as Staff
2. Search for patient "John Doe"
3. Select provider and available slot
4. Confirm booking on behalf of patient
5. Verify appointment record linked to patient
6. Verify confirmation email sent to patient (not staff)

**Expected Result**: Staff successfully books appointment for patient  
**Traceability**: FR-008

---

#### TC-APPT-004: Staff Creates Walk-in Booking (FR-009, FR-010, FR-011)
**Priority**: High  
**Use Case**: UC-003, UC-014  
**Test Steps**:
1. Authenticate as Staff
2. Select "Walk-in" booking option
3. Search for existing patient → not found
4. Create new patient account with basic demographics
5. Assign to same-day queue
6. Verify walk-in appointment created with timestamp
7. Mark patient as "Arrived"
8. Verify arrival timestamp recorded

**Expected Result**: Walk-in registered; patient in queue; arrival logged  
**Traceability**: FR-009, FR-010, FR-011

---

#### TC-APPT-005: Prevent Patient Self-Check-In (FR-012)
**Priority**: High  
**Test Steps**:
1. Authenticate as Patient with upcoming appointment
2. Navigate to appointment details
3. Verify no "Check-In" button or QR code option exists
4. Attempt direct API call to check-in endpoint as patient → expect 403 Forbidden

**Expected Result**: Patients cannot self-check-in; only staff can mark arrival  
**Traceability**: FR-012

---

### 4.3 Preferred Slot Swap / Waitlist (FR-014 to FR-017)

#### TC-SWAP-001: Patient Registers Preferred Slot Preference (FR-014)
**Priority**: High  
**Use Case**: UC-001, UC-004  
**Test Steps**:
1. Authenticate as Patient
2. Book available slot "2:00 PM"
3. During booking flow, select preferred slot "10:00 AM" (currently unavailable)
4. Confirm booking
5. Verify appointment at 2:00 PM
6. Verify preferred slot preference stored in database

**Expected Result**: Appointment booked at available slot; preference for unavailable slot registered  
**Traceability**: FR-014

---

#### TC-SWAP-002: System Automatically Swaps to Preferred Slot (FR-015, FR-016, FR-017)
**Priority**: High  
**Use Case**: UC-004  
**Test Steps**:
1. Complete TC-SWAP-001 (patient has 2:00 PM, prefers 10:00 AM)
2. As different patient: Cancel 10:00 AM appointment
3. Verify system detects 10:00 AM became available
4. Verify system swaps first patient to 10:00 AM
5. Verify original 2:00 PM slot released to available pool
6. Verify SMS and email notification sent to patient
7. Verify external calendar updated (if connected)

**Expected Result**: Automatic swap executed; original slot released; patient notified  
**Traceability**: FR-015, FR-016, FR-017

---

#### TC-SWAP-003: Multiple Patients Prefer Same Slot - FIFO Priority (UC-004 Alternative Flow)
**Priority**: Medium  
**Test Steps**:
1. Patient A books 1:00 PM, prefers 9:00 AM (timestamp T1)
2. Patient B books 3:00 PM, prefers 9:00 AM (timestamp T2, T2 > T1)
3. Cancel 9:00 AM appointment
4. Verify Patient A (earliest registrant) gets 9:00 AM
5. Verify Patient B remains at 3:00 PM with preference still active

**Expected Result**: Earliest registrant receives the swap  
**Traceability**: UC-004 Alternative Flow 3a

---

### 4.4 Reminders and Notifications (FR-018 to FR-021)

#### TC-NOTIF-001: System Sends SMS Reminder (FR-018, FR-021)
**Priority**: High  
**Use Case**: UC-011  
**Test Steps**:
1. Configure reminder schedule: 24 hours before appointment
2. Book appointment for 2026-06-12 10:00 AM
3. Wait until 2026-06-11 10:00 AM (24h before)
4. Verify SMS sent to patient's registered phone
5. Verify delivery status logged

**Expected Result**: SMS reminder sent at configured time; delivery logged  
**Traceability**: FR-018, FR-021

---

#### TC-NOTIF-002: System Sends Email Reminder (FR-019)
**Priority**: High  
**Use Case**: UC-011  
**Test Steps**:
1. Configure reminder schedule: 2 hours before appointment
2. Book appointment for 2026-06-12 10:00 AM
3. Wait until 2026-06-12 08:00 AM
4. Verify email reminder sent to patient
5. Verify email contains appointment details (provider, time, location)

**Expected Result**: Email reminder sent with complete appointment details  
**Traceability**: FR-019

---

#### TC-NOTIF-003: No-Show Risk Assessment (FR-020)
**Priority**: Medium  
**Use Case**: UC-011  
**Test Steps**:
1. Book appointment for patient with history of 2+ no-shows
2. Verify system calculates high no-show risk score
3. Verify additional reminder sent (or escalation to staff)
4. Book appointment for patient with 100% attendance
5. Verify standard reminder sent (no escalation)

**Expected Result**: High-risk patients receive enhanced reminders  
**Traceability**: FR-020

---

### 4.5 Calendar Integration (FR-022 to FR-024)

#### TC-CAL-001: Sync Appointment to Google Calendar (FR-022, FR-024)
**Priority**: Medium  
**Use Case**: UC-015  
**Test Steps**:
1. Patient connects Google Calendar in profile settings
2. Book appointment for 2026-06-15 10:00 AM
3. Verify API call to Google Calendar API with event details
4. Verify event appears in patient's Google Calendar
5. Modify appointment to 11:00 AM
6. Verify Google Calendar event updated
7. Cancel appointment
8. Verify Google Calendar event deleted

**Expected Result**: All appointment changes synced to Google Calendar  
**Traceability**: FR-022, FR-024

---

#### TC-CAL-002: Sync Appointment to Outlook Calendar (FR-023, FR-024)
**Priority**: Medium  
**Use Case**: UC-015  
**Test Steps**:
1. Patient connects Outlook Calendar via Microsoft Graph API
2. Book appointment for 2026-06-16 2:00 PM
3. Verify event created in Outlook Calendar
4. Verify event contains appointment details (provider, location)

**Expected Result**: Appointment synced to Outlook Calendar  
**Traceability**: FR-023, FR-024

---

#### TC-CAL-003: Calendar Sync Failure Handling (UC-015 Alternative Flow)
**Priority**: Medium  
**Test Steps**:
1. Simulate calendar API unavailability (network error)
2. Book appointment
3. Verify system logs sync failure
4. Verify retry with exponential backoff
5. Verify max retries reached → final failure logged
6. Verify appointment still created (sync failure doesn't block booking)

**Expected Result**: Graceful degradation; appointment booking succeeds even if sync fails  
**Traceability**: UC-015 Alternative Flow 4a

---

### 4.6 Insurance Pre-Check (FR-025 to FR-026)

#### TC-INS-001: Valid Insurance Record Match (FR-025, FR-026)
**Priority**: Medium  
**Use Case**: UC-010  
**Test Steps**:
1. Pre-seed database with dummy insurance record: "BlueCross", ID="BC123456"
2. Patient enters insurance: Name="BlueCross", ID="BC123456"
3. System queries internal insurance records
4. Verify result: "Valid"
5. Verify validation status saved to patient profile

**Expected Result**: Valid insurance recognized and confirmed  
**Traceability**: FR-025, FR-026

---

#### TC-INS-002: Invalid Insurance ID (FR-025, FR-026)
**Priority**: Medium  
**Use Case**: UC-010 Alternative Flow 4a  
**Test Steps**:
1. Pre-seed database with "BlueCross", ID="BC123456"
2. Patient enters "BlueCross", ID="BC999999" (wrong ID)
3. Verify result: "Invalid"
4. Verify guidance message displayed

**Expected Result**: Invalid ID detected; user notified  
**Traceability**: FR-025, FR-026

---

#### TC-INS-003: Insurance Not Found (FR-025, FR-026)
**Priority**: Medium  
**Use Case**: UC-010 Alternative Flow 3a  
**Test Steps**:
1. Patient enters "UnknownInsurance", ID="UNK12345"
2. Verify result: "Not Found"
3. Verify message: "Please contact staff for manual verification"

**Expected Result**: Unknown insurance flagged for staff review  
**Traceability**: FR-025, FR-026

---

### 4.7 Patient Intake (FR-027 to FR-031)

#### TC-INTAKE-001: AI Conversational Intake Mode (FR-027, FR-029, FR-031)
**Priority**: High  
**Use Case**: UC-005  
**Test Steps**:
1. Authenticate as Patient with upcoming appointment
2. Access intake section
3. Select "AI Conversational Mode"
4. AI asks: "Do you have any chronic conditions?"
5. Patient responds: "I have diabetes and hypertension"
6. Verify AI parses response → populates medical_history field
7. AI asks: "What medications are you currently taking?"
8. Patient responds: "Metformin 500mg twice daily"
9. Verify medication list populated
10. Review summary → confirm intake data
11. Verify data persisted to database

**Expected Result**: Conversational intake captures structured data  
**Traceability**: FR-027, FR-029, FR-031

---

#### TC-INTAKE-002: Manual Form Intake Mode (FR-028, FR-031)
**Priority**: High  
**Use Case**: UC-006  
**Test Steps**:
1. Authenticate as Patient
2. Access intake section
3. Select "Manual Form Mode"
4. Fill in fields: Medical History, Current Medications, Allergies, Current Symptoms
5. Submit form
6. Verify required field validation
7. Verify data persisted

**Expected Result**: Manual form captures structured data  
**Traceability**: FR-028, FR-031

---

#### TC-INTAKE-003: Switch Between AI and Manual Modes (FR-029)
**Priority**: High  
**Use Case**: UC-005 Alternative Flow 1a, UC-006 Alternative Flow 1a  
**Test Steps**:
1. Start in AI mode
2. Answer 3 questions → partial data captured
3. Switch to Manual Form mode
4. Verify previously captured data pre-populated in form
5. Complete remaining fields manually
6. Switch back to AI mode
7. Verify all data preserved
8. Complete and submit

**Expected Result**: Seamless mode switching; data preserved across modes  
**Traceability**: FR-029

---

#### TC-INTAKE-004: Patient Edits Submitted Intake (FR-030)
**Priority**: High  
**Test Steps**:
1. Complete intake submission (either mode)
2. Navigate to intake summary
3. Click "Edit" button
4. Modify medication: change "Metformin 500mg" to "Metformin 1000mg"
5. Save changes
6. Verify updated data in database
7. Verify no staff intervention required

**Expected Result**: Patients can self-edit intake data post-submission  
**Traceability**: FR-030

---

### 4.8 Clinical Document Management (FR-032 to FR-035)

#### TC-DOC-001: Patient Uploads PDF Clinical Document (FR-032, FR-034, FR-035)
**Priority**: High  
**Use Case**: UC-007  
**Test Steps**:
1. Authenticate as Patient
2. Navigate to document upload section
3. Select PDF file containing lab results
4. Upload document
5. Verify file stored with HIPAA-compliant encryption
6. Verify document queued for AI extraction
7. Wait for extraction processing
8. Verify extracted data (e.g., vitals: BP=120/80, glucose=95 mg/dL)
9. Verify extracted data integrated into patient profile

**Expected Result**: PDF uploaded; data extracted; profile updated  
**Traceability**: FR-032, FR-034, FR-035

---

#### TC-DOC-002: Invalid File Format Rejected (UC-007 Alternative Flow 2a)
**Priority**: High  
**Test Steps**:
1. Attempt to upload .docx file
2. Verify system rejects upload
3. Verify error message: "Only PDF format supported"

**Expected Result**: Non-PDF files rejected with clear error message  
**Traceability**: UC-007 Alternative Flow 2a

---

#### TC-DOC-003: System Ingests Post-Visit Clinical Notes (FR-033)
**Priority**: High  
**Test Steps**:
1. Simulate post-visit note ingestion (staff uploads or EHR integration)
2. Verify note stored securely
3. Verify note queued for extraction
4. Verify extracted data includes diagnoses, procedures, prescriptions

**Expected Result**: Post-visit notes processed for data extraction  
**Traceability**: FR-033

---

### 4.9 360-Degree Patient View (FR-036 to FR-040)

#### TC-360-001: Generate Unified Patient View (FR-036, FR-040)
**Priority**: High  
**Use Case**: UC-008  
**Test Steps**:
1. Upload 3 clinical documents for patient:
   - Lab results (2025-01-15): BP=130/85, glucose=100
   - Physician note (2025-03-10): BP=125/80, medications=[Metformin, Lisinopril]
   - Recent labs (2025-06-01): glucose=92, cholesterol=180
2. Trigger 360-Degree View generation
3. Start timer
4. Verify view generated within 2 minutes
5. Verify consolidated vitals displayed (latest values prioritized)
6. Verify consolidated medication list
7. Verify data sources attributed to each value

**Expected Result**: Unified view generated in <2 minutes with all data consolidated  
**Traceability**: FR-036, FR-040, NFR-005

---

#### TC-360-002: De-Duplication Across Sources (FR-037)
**Priority**: High  
**Use Case**: UC-008  
**Test Steps**:
1. Document A lists medication "Metformin 500mg"
2. Document B lists medication "Metformin 500mg"
3. Document C lists medication "Metformin 1000mg" (different dose)
4. Generate 360-Degree View
5. Verify "Metformin 500mg" appears once (not duplicated)
6. Verify "Metformin 1000mg" appears as separate entry (different dose)

**Expected Result**: Exact duplicates removed; similar-but-different entries preserved  
**Traceability**: FR-037

---

#### TC-360-003: Conflict Detection and Highlighting (FR-038)
**Priority**: High  
**Use Case**: UC-008, UC-009  
**Test Steps**:
1. Document A: Allergies="Penicillin"
2. Document B: Allergies="None reported"
3. Generate 360-Degree View
4. Verify conflict highlighted with both values shown
5. Verify source documents referenced
6. Verify conflict marked as "Critical" (allergy conflict)

**Expected Result**: Conflicts explicitly highlighted with source attribution  
**Traceability**: FR-038

---

#### TC-360-004: Staff Resolves Data Conflict (FR-039)
**Priority**: High  
**Use Case**: UC-009  
**Test Steps**:
1. Complete TC-360-003 (allergy conflict exists)
2. Authenticate as Staff
3. Open patient 360-Degree View
4. Select allergy conflict
5. Review both values with source documents
6. Select correct value: "Penicillin"
7. Save resolution
8. Verify conflict removed from view
9. Verify resolution logged in audit trail with staff ID and timestamp

**Expected Result**: Conflict resolved; audit trail updated  
**Traceability**: FR-039

---

### 4.10 Medical Coding (FR-041 to FR-044)

#### TC-CODE-001: System Maps Diagnosis to ICD-10 (FR-041, FR-043)
**Priority**: High  
**Use Case**: UC-013  
**Test Steps**:
1. Patient profile contains diagnosis: "Type 2 Diabetes Mellitus"
2. Trigger medical coding process
3. Verify system suggests ICD-10 code: E11.9
4. Verify confidence score displayed (e.g., 0.95)
5. Staff reviews and confirms code

**Expected Result**: ICD-10 code suggested with confidence indicator  
**Traceability**: FR-041, FR-043

---

#### TC-CODE-002: System Maps Procedure to CPT (FR-042, FR-043)
**Priority**: High  
**Use Case**: UC-013  
**Test Steps**:
1. Clinical note mentions: "Complete Blood Count performed"
2. Trigger medical coding
3. Verify system suggests CPT code: 85025
4. Verify confidence score displayed
5. Staff confirms code

**Expected Result**: CPT code suggested with confidence score  
**Traceability**: FR-042, FR-043

---

#### TC-CODE-003: AI-Human Agreement Rate Validation (FR-044)
**Priority**: High  
**Test Steps**:
1. Prepare ground truth dataset: 100 clinical scenarios with expert-assigned codes
2. Run coding system on all 100 scenarios
3. Compare system suggestions to expert codes
4. Calculate agreement rate: (matches / total) * 100
5. Verify agreement rate > 98%

**Expected Result**: System achieves >98% agreement with expert coders  
**Traceability**: FR-044

---

#### TC-CODE-004: Low Confidence Code Flagged for Review (UC-013 Alternative Flow 4a)
**Priority**: Medium  
**Test Steps**:
1. System suggests code with confidence=0.65 (below threshold)
2. Verify code flagged for mandatory staff review
3. Verify visual indicator (e.g., yellow highlight)

**Expected Result**: Low-confidence codes require manual verification  
**Traceability**: UC-013 Alternative Flow 4a

---

### 4.11 Audit and Compliance (FR-045 to FR-048)

#### TC-AUDIT-001: Immutable Audit Log for Patient Data Access (FR-045)
**Priority**: High  
**Test Steps**:
1. Staff views patient record for Patient ID=12345
2. Query audit log table
3. Verify entry: {user_id, patient_id, action='VIEW', timestamp, ip_address}
4. Attempt to modify audit log record (UPDATE statement)
5. Verify operation fails or triggers alert

**Expected Result**: All access logged; logs cannot be modified  
**Traceability**: FR-045

---

#### TC-AUDIT-002: Immutable Audit Log for Staff Actions (FR-046)
**Priority**: High  
**Test Steps**:
1. Staff marks patient as "Arrived"
2. Verify audit log entry: {user_id, action='MARK_ARRIVED', patient_id, timestamp}
3. Admin creates new user
4. Verify audit log entry: {admin_id, action='CREATE_USER', target_user_id, timestamp}

**Expected Result**: All staff actions logged immutably  
**Traceability**: FR-046

---

#### TC-AUDIT-003: HIPAA-Compliant Data Handling (FR-047, NFR-003)
**Priority**: High  
**Test Steps**:
1. Verify all patient data fields encrypted in database
2. Verify API responses use HTTPS (TLS 1.2+)
3. Verify audit logs capture all PHI access
4. Verify role-based access enforced for PHI
5. Verify session timeouts enforced
6. Verify data breach notification procedure documented

**Expected Result**: Full HIPAA compliance demonstrated  
**Traceability**: FR-047, NFR-003

---

#### TC-AUDIT-004: Encryption In-Transit and At-Rest (FR-048, NFR-001, NFR-002)
**Priority**: High  
**Test Steps**:
1. Intercept API call with network sniffer → verify HTTPS/TLS 1.2+
2. Query database for patient record → verify encrypted fields (AES-256)
3. Verify encryption keys stored in secure key management system (not in code)

**Expected Result**: All data encrypted in-transit (TLS 1.2+) and at-rest (AES-256)  
**Traceability**: FR-048, NFR-001, NFR-002

---

### 4.12 Administration (FR-049 to FR-051)

#### TC-ADMIN-001: Admin Creates and Deactivates Users (FR-049)
**Priority**: High  
**Use Case**: UC-012  
**Test Steps**:
1. Authenticate as Admin
2. Create new Staff user "Jane Doe"
3. Verify account created with role='Staff'
4. Verify activation email sent
5. Deactivate user "Jane Doe"
6. Verify user cannot authenticate
7. Verify access immediately revoked
8. Verify deactivation logged in audit trail

**Expected Result**: Admin can create and deactivate users; actions audited  
**Traceability**: FR-049

---

#### TC-ADMIN-002: Admin Assigns and Modifies Roles (FR-050)
**Priority**: High  
**Use Case**: UC-012  
**Test Steps**:
1. Authenticate as Admin
2. Create user "John Smith" with role='Patient'
3. Update role to 'Staff'
4. Verify user now has Staff permissions
5. Verify role change logged in audit trail

**Expected Result**: Admin can modify user roles; changes audited  
**Traceability**: FR-050

---

#### TC-ADMIN-003: Admin Access to Audit Logs and Reports (FR-051)
**Priority**: Medium  
**Test Steps**:
1. Authenticate as Admin
2. Navigate to audit log viewer
3. Filter logs by date range: 2026-06-01 to 2026-06-10
4. Verify all logged actions displayed
5. Export audit log to CSV
6. Verify export contains all records

**Expected Result**: Admin can view and export audit logs  
**Traceability**: FR-051

---

## 5. Use Case Test Scenarios

### UC-001: Patient Books Appointment
**Coverage**: TC-APPT-001, TC-APPT-002, TC-SWAP-001  
**Scenarios**:
- Happy path: Select provider, date, available slot → booking succeeds
- Preferred slot: Book available slot + register preferred unavailable slot
- No slots available: Display waitlist option
- Concurrent booking: Two patients attempt to book same slot → first request succeeds, second fails gracefully

---

### UC-002: Staff Books Appointment for Patient
**Coverage**: TC-APPT-003  
**Scenarios**:
- Existing patient: Staff searches, finds patient, books appointment
- New patient: Staff creates patient account first, then books

---

### UC-003: Staff Registers Walk-in Patient
**Coverage**: TC-APPT-004  
**Scenarios**:
- Walk-in with existing account: Link to same-day queue
- Walk-in without account: Create account + queue assignment
- Walk-in without account creation: One-time visit record

---

### UC-004: Preferred Slot Swap Execution
**Coverage**: TC-SWAP-002, TC-SWAP-003  
**Scenarios**:
- Single patient prefers slot: Slot opens → swap executes
- Multiple patients prefer slot: FIFO priority applied
- Patient cancels before swap: Preference discarded

---

### UC-005: Patient Completes AI Conversational Intake
**Coverage**: TC-INTAKE-001, TC-INTAKE-003  
**Scenarios**:
- Full conversational completion
- Switch to manual form mid-conversation
- Edit AI-captured data before submission

---

### UC-006: Patient Completes Manual Form Intake
**Coverage**: TC-INTAKE-002, TC-INTAKE-003  
**Scenarios**:
- Full manual form completion
- Switch to AI mode mid-form
- Validation errors → correction loop

---

### UC-007: Patient Uploads Clinical Document
**Coverage**: TC-DOC-001, TC-DOC-002  
**Scenarios**:
- Valid PDF upload → extraction succeeds
- Invalid file format → rejection with error
- Large PDF (>10MB) → compression or rejection
- Extraction ambiguity → staff review flag

---

### UC-008: System Generates 360-Degree Patient View
**Coverage**: TC-360-001, TC-360-002, TC-360-003  
**Scenarios**:
- Single document → simple view
- Multiple documents → de-duplication + consolidation
- Conflicting data → conflict highlighting

---

### UC-009: Staff Resolves Data Conflict
**Coverage**: TC-360-004  
**Scenarios**:
- Resolve with existing value selection
- Resolve with manual reconciliation (new value)
- Defer resolution → conflict remains highlighted

---

### UC-010: System Performs Insurance Pre-Check
**Coverage**: TC-INS-001, TC-INS-002, TC-INS-003  
**Scenarios**:
- Exact match → Valid
- Name match, ID mismatch → Invalid
- No match → Not Found

---

### UC-011: System Sends Appointment Reminders
**Coverage**: TC-NOTIF-001, TC-NOTIF-002, TC-NOTIF-003  
**Scenarios**:
- Standard patient: SMS + Email at configured times
- High no-show risk: Additional reminders
- Delivery failure: Retry logic

---

### UC-012: Admin Manages User Accounts
**Coverage**: TC-ADMIN-001, TC-ADMIN-002  
**Scenarios**:
- Create Staff/Admin accounts
- Deactivate user
- Modify user role
- Duplicate email prevention

---

### UC-013: System Performs Medical Coding
**Coverage**: TC-CODE-001, TC-CODE-002, TC-CODE-003, TC-CODE-004  
**Scenarios**:
- High confidence code → auto-suggest
- Low confidence code → mandatory review
- Multiple applicable codes → suggest top 3 with confidence

---

### UC-014: Staff Marks Patient as Arrived
**Coverage**: TC-APPT-004  
**Scenarios**:
- Scheduled appointment arrival
- Walk-in arrival
- Early/late arrival timestamp capture

---

### UC-015: System Syncs with External Calendar
**Coverage**: TC-CAL-001, TC-CAL-002, TC-CAL-003  
**Scenarios**:
- Google Calendar create/update/delete
- Outlook Calendar create/update/delete
- Sync failure → retry with backoff

---

## 6. Non-Functional Requirements Test Coverage

### 6.1 Security (NFR-001, NFR-002, NFR-003)

#### TC-NFR-SEC-001: TLS 1.2+ Encryption In-Transit (NFR-001)
**Test Steps**:
1. Use SSL Labs or OpenSSL to analyze API endpoint
2. Verify TLS version ≥ 1.2
3. Verify strong cipher suites enabled
4. Verify no SSL/TLS vulnerabilities

**Expected Result**: All API communication uses TLS 1.2+  
**Tools**: SSL Labs, OpenSSL

---

#### TC-NFR-SEC-002: AES-256 Encryption At-Rest (NFR-002)
**Test Steps**:
1. Query database for encrypted patient data fields
2. Verify encryption algorithm = AES-256
3. Verify encryption keys stored in Azure Key Vault or similar (not in code/config)

**Expected Result**: All patient data encrypted at-rest with AES-256  
**Tools**: Database inspection, security audit

---

#### TC-NFR-SEC-003: HIPAA Compliance Audit (NFR-003)
**Test Steps**:
1. Conduct HIPAA Security Rule checklist review (45 CFR §164.312)
2. Verify administrative, physical, and technical safeguards
3. Verify Business Associate Agreements (if applicable)
4. Verify breach notification procedures

**Expected Result**: 100% HIPAA compliance  
**Tools**: HIPAA compliance checklist, penetration testing

---

### 6.2 Performance (NFR-004, NFR-005)

#### TC-NFR-PERF-001: System Uptime 99.9% (NFR-004)
**Test Steps**:
1. Monitor system uptime over 30-day period
2. Calculate uptime percentage: (total_time - downtime) / total_time * 100
3. Verify uptime ≥ 99.9%

**Expected Result**: <43 minutes downtime per month  
**Tools**: UptimeRobot, Pingdom, application monitoring

---

#### TC-NFR-PERF-002: 360-Degree View Generation <2 Minutes (NFR-005)
**Test Steps**:
1. Prepare patient profile with 10 uploaded documents
2. Trigger 360-Degree View generation
3. Measure time from trigger to view availability
4. Verify time < 2 minutes
5. Repeat with 5, 20, 50 documents

**Expected Result**: View generation completes in <2 minutes regardless of document count (within reasonable limits)  
**Tools**: Performance profiling, APM tools

---

#### TC-NFR-PERF-003: Load Testing - 100 Concurrent Users
**Test Steps**:
1. Simulate 100 concurrent users booking appointments
2. Measure response times (p50, p95, p99)
3. Verify p95 response time < 2 seconds
4. Verify zero errors

**Expected Result**: System handles 100 concurrent users with acceptable performance  
**Tools**: k6, Apache JMeter

---

#### TC-NFR-PERF-004: Load Testing - 500 Concurrent Users
**Test Steps**:
1. Simulate 500 concurrent users (mix of booking, intake, document upload)
2. Measure response times and error rates
3. Verify p95 < 3 seconds
4. Verify error rate < 1%

**Expected Result**: System scales to 500 concurrent users  
**Tools**: k6, Apache JMeter

---

### 6.3 Session Management (NFR-006)

#### TC-NFR-SESSION-001: 15-Minute Session Timeout (NFR-006)
**Coverage**: TC-AUTH-004  
**Expected Result**: Sessions expire after 15 minutes inactivity  

---

### 6.4 Infrastructure (NFR-007 to NFR-010)

#### TC-NFR-INFRA-001: Windows Services/IIS Deployment (NFR-007)
**Test Steps**:
1. Deploy .NET API to Windows Server with IIS
2. Verify application pool configured correctly
3. Verify HTTPS bindings
4. Verify service starts automatically
5. Perform smoke tests (health check endpoint)

**Expected Result**: API successfully deployed on Windows/IIS  
**Tools**: IIS Manager, PowerShell

---

#### TC-NFR-INFRA-002: PostgreSQL Database Connectivity (NFR-008)
**Test Steps**:
1. Configure connection string to PostgreSQL instance
2. Run EF Core migrations
3. Verify all tables created
4. Perform CRUD operations
5. Verify data persistence

**Expected Result**: Application connects to PostgreSQL; migrations succeed  
**Tools**: pgAdmin, psql

---

#### TC-NFR-INFRA-003: Upstash Redis Caching (NFR-009)
**Test Steps**:
1. Configure Upstash Redis connection
2. Cache appointment availability data
3. Verify cache hit/miss metrics
4. Verify cache expiration policy

**Expected Result**: Redis caching functional; reduces database load  
**Tools**: Redis CLI, Upstash dashboard

---

#### TC-NFR-INFRA-004: Free Tier Hosting (NFR-010)
**Test Steps**:
1. Deploy frontend to Netlify/Vercel free tier
2. Deploy backend API to compatible free hosting
3. Verify all services communicate correctly
4. Verify no paid services used

**Expected Result**: Full system operational on free-tier infrastructure  
**Platforms**: Netlify, Vercel, GitHub Codespaces

---

### 6.5 Technology Stack (NFR-011, NFR-012, NFR-013)

#### TC-NFR-TECH-001: Angular Frontend Functional (NFR-011)
**Test Steps**:
1. Verify Angular version ≥ 17
2. Verify standalone components architecture
3. Verify lazy loading implemented
4. Run `ng build --prod` → verify no errors

**Expected Result**: Angular frontend builds and runs correctly  
**Tools**: Angular CLI

---

#### TC-NFR-TECH-002: .NET Backend API Functional (NFR-012)
**Test Steps**:
1. Verify .NET version = 8
2. Verify Clean Architecture structure (API, Application, Domain, Infrastructure)
3. Verify Swagger/OpenAPI documentation available
4. Run `dotnet build` → verify no errors

**Expected Result**: .NET API builds and exposes documented endpoints  
**Tools**: .NET CLI, Swagger UI

---

#### TC-NFR-TECH-003: Free and Open-Source Tools Only (NFR-013)
**Test Steps**:
1. Audit all project dependencies (NuGet, npm)
2. Verify no paid/proprietary libraries
3. Audit infrastructure components
4. Verify no paid cloud services

**Expected Result**: All tools and services are free/open-source  
**Tools**: Dependency scanning, license audit

---

## 7. Test Data Requirements

### 7.1 User Accounts

| Role | Count | Attributes |
|------|-------|------------|
| Patient | 50 | Varied demographics, appointment histories, no-show rates |
| Staff | 10 | Different departments, permissions |
| Admin | 3 | Full system access |

### 7.2 Appointment Data

- **Providers**: 5 providers with varying specialties
- **Slots**: 200+ appointment slots across 2-week period
- **Bookings**: Mix of confirmed, cancelled, completed, no-show

### 7.3 Clinical Documents

- **Lab Results**: 20 PDFs with structured data (vitals, test results)
- **Physician Notes**: 15 PDFs with unstructured narratives
- **Imaging Reports**: 10 PDFs with mixed content
- **Prescription Records**: 10 PDFs

### 7.4 Insurance Records

- **Valid Insurances**: 10 dummy records (name + ID pairs)
- **Invalid Test Cases**: 5 records with mismatched IDs
- **Unknown Insurances**: Random test data

### 7.5 Ground Truth Datasets

- **Medical Coding**: 100 clinical scenarios with expert-assigned ICD-10/CPT codes
- **Data Extraction**: 50 annotated documents with expected extraction results

---

## 8. Test Execution Plan

### 8.1 Test Phases

| Phase | Timeline | Focus | Exit Criteria |
|-------|----------|-------|---------------|
| **Phase 1: Unit Testing** | Weeks 1-2 | Individual components | 80%+ code coverage; all unit tests pass |
| **Phase 2: Integration Testing** | Weeks 3-4 | API endpoints, service integrations | All integration tests pass; no critical defects |
| **Phase 3: System Testing** | Weeks 5-6 | End-to-end workflows, cross-stack | All use cases validated; performance baselines met |
| **Phase 4: Acceptance Testing** | Week 7 | Business use case validation | Product Owner sign-off |
| **Phase 5: Performance Testing** | Week 8 | Load, stress, scalability | NFR-004, NFR-005 validated |
| **Phase 6: Security Testing** | Week 8 | OWASP, HIPAA compliance | Zero high/critical security vulnerabilities |
| **Phase 7: Regression Testing** | Ongoing | Continuous validation | All regression tests pass post-deployment |

### 8.2 Test Automation Strategy

| Layer | Automation | Tools | Target Coverage |
|-------|------------|-------|-----------------|
| Unit Tests | 100% automated | xUnit, Jasmine, pytest | 80%+ code coverage |
| Integration Tests | 100% automated | xUnit, Postman | 100% API endpoint coverage |
| System Tests | 80% automated | Playwright, Selenium | Critical user journeys |
| Performance Tests | 100% automated | k6, JMeter | Load/stress scenarios |
| Security Tests | 50% automated | OWASP ZAP | OWASP Top 10 scans |

### 8.3 Defect Management

| Severity | SLA | Criteria |
|----------|-----|----------|
| **Critical** | 24 hours | System down, data loss, security breach |
| **High** | 3 days | Major feature broken, blocking issue |
| **Medium** | 1 week | Minor feature issue, workaround available |
| **Low** | 2 weeks | Cosmetic issue, enhancement request |

---

## 9. Traceability Matrix

### 9.1 Functional Requirements Coverage

| FR ID | Requirement Summary | Test Cases | Status |
|-------|---------------------|------------|--------|
| FR-001 | Patient self-registration | TC-AUTH-001 | ✅ Covered |
| FR-002 | Admin creates accounts | TC-AUTH-002 | ✅ Covered |
| FR-003 | RBAC enforcement | TC-AUTH-003 | ✅ Covered |
| FR-004 | 15-min session timeout | TC-AUTH-004 | ✅ Covered |
| FR-005 | Secure credential storage | TC-AUTH-005 | ✅ Covered |
| FR-006 | Search available slots | TC-APPT-001 | ✅ Covered |
| FR-007 | Book appointment | TC-APPT-002 | ✅ Covered |
| FR-008 | Staff books for patient | TC-APPT-003 | ✅ Covered |
| FR-009 | Walk-in bookings | TC-APPT-004 | ✅ Covered |
| FR-010 | Same-day queue | TC-APPT-004 | ✅ Covered |
| FR-011 | Mark arrived | TC-APPT-004 | ✅ Covered |
| FR-012 | Prevent self-check-in | TC-APPT-005 | ✅ Covered |
| FR-013 | Confirmation PDF | TC-APPT-002 | ✅ Covered |
| FR-014 | Preferred slot selection | TC-SWAP-001 | ✅ Covered |
| FR-015 | Auto swap to preferred | TC-SWAP-002 | ✅ Covered |
| FR-016 | Release original slot | TC-SWAP-002 | ✅ Covered |
| FR-017 | Swap notification | TC-SWAP-002 | ✅ Covered |
| FR-018 | SMS reminders | TC-NOTIF-001 | ✅ Covered |
| FR-019 | Email reminders | TC-NOTIF-002 | ✅ Covered |
| FR-020 | No-show risk assessment | TC-NOTIF-003 | ✅ Covered |
| FR-021 | Configurable reminder timing | TC-NOTIF-001 | ✅ Covered |
| FR-022 | Google Calendar sync | TC-CAL-001 | ✅ Covered |
| FR-023 | Outlook Calendar sync | TC-CAL-002 | ✅ Covered |
| FR-024 | Calendar update on change | TC-CAL-001, TC-CAL-002 | ✅ Covered |
| FR-025 | Insurance validation | TC-INS-001, TC-INS-002 | ✅ Covered |
| FR-026 | Display validation result | TC-INS-001, TC-INS-002, TC-INS-003 | ✅ Covered |
| FR-027 | AI conversational intake | TC-INTAKE-001 | ✅ Covered |
| FR-028 | Manual form intake | TC-INTAKE-002 | ✅ Covered |
| FR-029 | Switch intake modes | TC-INTAKE-003 | ✅ Covered |
| FR-030 | Patient edits intake | TC-INTAKE-004 | ✅ Covered |
| FR-031 | Persist intake data | TC-INTAKE-001, TC-INTAKE-002 | ✅ Covered |
| FR-032 | Upload PDF documents | TC-DOC-001 | ✅ Covered |
| FR-033 | Ingest clinical notes | TC-DOC-003 | ✅ Covered |
| FR-034 | Extract structured data | TC-DOC-001 | ✅ Covered |
| FR-035 | Aggregate data | TC-DOC-001 | ✅ Covered |
| FR-036 | Generate 360-Degree View | TC-360-001 | ✅ Covered |
| FR-037 | De-duplication | TC-360-002 | ✅ Covered |
| FR-038 | Highlight conflicts | TC-360-003 | ✅ Covered |
| FR-039 | Conflict resolution | TC-360-004 | ✅ Covered |
| FR-040 | 20min → 2min task | TC-360-001 | ✅ Covered |
| FR-041 | ICD-10 mapping | TC-CODE-001 | ✅ Covered |
| FR-042 | CPT mapping | TC-CODE-002 | ✅ Covered |
| FR-043 | Confidence indicators | TC-CODE-001, TC-CODE-002 | ✅ Covered |
| FR-044 | >98% AI-Human agreement | TC-CODE-003 | ✅ Covered |
| FR-045 | Audit logs - data access | TC-AUDIT-001 | ✅ Covered |
| FR-046 | Audit logs - staff actions | TC-AUDIT-002 | ✅ Covered |
| FR-047 | HIPAA compliance | TC-AUDIT-003 | ✅ Covered |
| FR-048 | Encryption in-transit/at-rest | TC-AUDIT-004 | ✅ Covered |
| FR-049 | Admin user management | TC-ADMIN-001 | ✅ Covered |
| FR-050 | Admin role assignment | TC-ADMIN-002 | ✅ Covered |
| FR-051 | Admin audit log access | TC-ADMIN-003 | ✅ Covered |

**Total Coverage**: 51/51 FRs = 100%

### 9.2 Use Case Coverage

| UC ID | Use Case | Test Scenarios | Status |
|-------|----------|----------------|--------|
| UC-001 | Patient Books Appointment | TC-APPT-001, TC-APPT-002, TC-SWAP-001 | ✅ Covered |
| UC-002 | Staff Books for Patient | TC-APPT-003 | ✅ Covered |
| UC-003 | Staff Registers Walk-in | TC-APPT-004 | ✅ Covered |
| UC-004 | Preferred Slot Swap | TC-SWAP-002, TC-SWAP-003 | ✅ Covered |
| UC-005 | AI Conversational Intake | TC-INTAKE-001, TC-INTAKE-003 | ✅ Covered |
| UC-006 | Manual Form Intake | TC-INTAKE-002, TC-INTAKE-003 | ✅ Covered |
| UC-007 | Upload Clinical Document | TC-DOC-001, TC-DOC-002 | ✅ Covered |
| UC-008 | Generate 360-Degree View | TC-360-001, TC-360-002, TC-360-003 | ✅ Covered |
| UC-009 | Resolve Data Conflict | TC-360-004 | ✅ Covered |
| UC-010 | Insurance Pre-Check | TC-INS-001, TC-INS-002, TC-INS-003 | ✅ Covered |
| UC-011 | Send Reminders | TC-NOTIF-001, TC-NOTIF-002, TC-NOTIF-003 | ✅ Covered |
| UC-012 | Admin Manages Users | TC-ADMIN-001, TC-ADMIN-002 | ✅ Covered |
| UC-013 | Medical Coding | TC-CODE-001, TC-CODE-002, TC-CODE-003, TC-CODE-004 | ✅ Covered |
| UC-014 | Mark Patient Arrived | TC-APPT-004 | ✅ Covered |
| UC-015 | Calendar Sync | TC-CAL-001, TC-CAL-002, TC-CAL-003 | ✅ Covered |

**Total Coverage**: 15/15 UCs = 100%

### 9.3 Non-Functional Requirements Coverage

| NFR ID | Requirement | Test Cases | Status |
|--------|-------------|------------|--------|
| NFR-001 | TLS 1.2+ encryption | TC-NFR-SEC-001, TC-AUDIT-004 | ✅ Covered |
| NFR-002 | AES-256 at-rest encryption | TC-NFR-SEC-002, TC-AUDIT-004 | ✅ Covered |
| NFR-003 | HIPAA compliance | TC-NFR-SEC-003, TC-AUDIT-003 | ✅ Covered |
| NFR-004 | 99.9% uptime | TC-NFR-PERF-001 | ✅ Covered |
| NFR-005 | <2min view generation | TC-NFR-PERF-002, TC-360-001 | ✅ Covered |
| NFR-006 | 15-min session timeout | TC-NFR-SESSION-001, TC-AUTH-004 | ✅ Covered |
| NFR-007 | Windows/IIS deployment | TC-NFR-INFRA-001 | ✅ Covered |
| NFR-008 | PostgreSQL database | TC-NFR-INFRA-002 | ✅ Covered |
| NFR-009 | Upstash Redis caching | TC-NFR-INFRA-003 | ✅ Covered |
| NFR-010 | Free-tier hosting | TC-NFR-INFRA-004 | ✅ Covered |
| NFR-011 | Angular frontend | TC-NFR-TECH-001 | ✅ Covered |
| NFR-012 | .NET backend | TC-NFR-TECH-002 | ✅ Covered |
| NFR-013 | Free/open-source tools | TC-NFR-TECH-003 | ✅ Covered |

**Total Coverage**: 13/13 NFRs = 100%

---

## 10. Risk Assessment

| Risk | Probability | Impact | Mitigation | Owner |
|------|-------------|--------|------------|-------|
| AI extraction accuracy <98% | Medium | High | Expand training dataset; implement human-in-loop review | AI/ML Team |
| Calendar API rate limits exceeded | Medium | Medium | Implement exponential backoff; cache sync status | Backend Team |
| PostgreSQL performance bottleneck | Low | High | Index optimization; query profiling; caching layer | Database Team |
| HIPAA audit failure | Low | Critical | Pre-audit compliance review; penetration testing | Security + QA |
| Free-tier infrastructure limitations | High | Medium | Monitor usage; plan for paid tier migration if needed | DevOps |
| Medical coding confidence threshold tuning | Medium | Medium | A/B testing; expert validation; incremental rollout | Product + QA |
| Session management edge cases | Low | Medium | Comprehensive session timeout testing; token refresh flow validation | Backend Team |
| Cross-browser compatibility issues | Medium | Low | Automated cross-browser testing (Chrome, Firefox, Safari, Edge) | Frontend + QA |

---

## 11. Test Deliverables

| Deliverable | Description | Owner | Due Date |
|-------------|-------------|-------|----------|
| **Master Test Plan** (this document) | Comprehensive test strategy and cases | QA Lead | 2026-06-10 ✅ |
| **Unit Test Suite** | xUnit, Jasmine, pytest test implementations | Development Team | 2026-06-20 |
| **Integration Test Suite** | API endpoint tests, service integration tests | Development Team | 2026-06-25 |
| **System Test Suite** | Playwright end-to-end test scripts | QA Team | 2026-06-30 |
| **Performance Test Scripts** | k6/JMeter load test scenarios | QA + DevOps | 2026-07-05 |
| **Security Test Report** | OWASP ZAP scan results, penetration test report | Security Team | 2026-07-10 |
| **UAT Test Plan & Results** | Acceptance test scenarios and sign-off | Product Owner + QA | 2026-07-15 |
| **Traceability Matrix** | FR/UC/NFR to test case mapping (§9) | QA Lead | 2026-06-10 ✅ |
| **Test Execution Report** | Daily/weekly test run summaries | QA Team | Ongoing |
| **Defect Log** | Bug tracker export with severity/status | QA Team | Ongoing |

---

## 12. Test Tool Stack

| Category | Tool | Purpose |
|----------|------|---------|
| **Unit Testing** | xUnit | .NET unit tests |
| **Unit Testing** | Jasmine/Karma | Angular unit tests |
| **Unit Testing** | pytest | Python AI service unit tests |
| **API Testing** | Postman | Manual API testing |
| **API Testing** | Newman | Automated Postman collection runs |
| **E2E Testing** | Playwright | Browser automation for system tests |
| **Performance Testing** | k6 | Load and stress testing |
| **Performance Testing** | Apache JMeter | Alternative load testing |
| **Security Testing** | OWASP ZAP | Automated vulnerability scanning |
| **Security Testing** | SonarQube | Static code analysis |
| **Monitoring** | UptimeRobot | Uptime monitoring (NFR-004) |
| **Test Management** | Azure DevOps / GitHub Issues | Test case management, defect tracking |
| **CI/CD** | GitHub Actions | Automated test execution on commits |

---

## 13. Acceptance Criteria

The system is considered **ready for production** when:

1. ✅ All 51 functional requirements validated (100% coverage)
2. ✅ All 15 use cases tested with positive/negative/edge cases
3. ✅ All 13 non-functional requirements met
4. ✅ Code coverage ≥80% for unit tests
5. ✅ Zero critical or high-severity defects in production environment
6. ✅ AI-Human Agreement Rate >98% for medical coding (FR-044)
7. ✅ 360-Degree Patient View generation <2 minutes (NFR-005, FR-040)
8. ✅ HIPAA compliance validated by external audit (NFR-003)
9. ✅ System uptime ≥99.9% over 30-day monitoring period (NFR-004)
10. ✅ Performance tests pass: 500 concurrent users with p95 <3s
11. ✅ Security tests pass: Zero high/critical OWASP vulnerabilities
12. ✅ Product Owner UAT sign-off obtained

---

## 14. Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **QA Lead** | [TBD] | _________________ | __________ |
| **Product Owner** | [TBD] | _________________ | __________ |
| **Development Lead** | [TBD] | _________________ | __________ |
| **Security Lead** | [TBD] | _________________ | __________ |

---

## 15. Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-06-10 | AI Assistant | Initial Master Test Plan created from spec.md v1.0 |

---

**End of Master Test Plan**
