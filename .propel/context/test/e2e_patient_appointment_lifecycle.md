# E2E Journey: Complete Patient Appointment Lifecycle

## Metadata

| Field | Value |
|-------|-------|
| **Journey** | Complete Patient Appointment Lifecycle |
| **Test Type** | End-to-End |
| **User Persona** | New Patient |
| **Requirements** | FR-001, FR-006, FR-007, FR-013, FR-014, FR-018, FR-019, FR-022, FR-027, FR-031 |
| **Use Cases** | UC-001, UC-005, UC-011, UC-015 |
| **Priority** | Critical |
| **Estimated Duration** | 8-10 minutes |
| **Test Framework** | Playwright + TypeScript |

---

## Journey Overview

This E2E journey validates the complete patient lifecycle from initial registration through appointment booking, intake completion, calendar sync, and reminder receipt. It represents the primary happy path for patient onboarding and appointment scheduling.

**Journey Phases**:
1. Patient Registration & Activation
2. Appointment Search & Booking
3. Preferred Slot Registration
4. AI-Assisted Intake Completion
5. Calendar Integration
6. Appointment Reminder Receipt
7. Appointment Attendance

---

## Pre-Conditions

```yaml
prerequisites:
  - clean_database: true
  - email_service_running: true
  - sms_service_running: true
  - calendar_api_available: true
  - provider_availability:
      provider: "Dr. Smith"
      date: "2026-06-20"
      available_slots: ["09:00 AM", "10:00 AM", "02:00 PM"]
      unavailable_slots: ["11:00 AM"]
```

---

## Journey Phases

### Phase 1: Patient Registration & Activation

**Objective**: New patient creates account and activates it  
**Requirements**: FR-001  
**Duration**: 2-3 minutes

```yaml
phase: "Registration & Activation"
steps:
  - checkpoint: "Navigate to Registration"
    actions:
      - goto: "/"
      - click: '[data-testid="register-link"]'
      - expectURL: "/register"
      
  - checkpoint: "Complete Registration Form"
    actions:
      - fill:
          selector: '[data-testid="name-input"]'
          value: "Emma Johnson"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "emma.johnson@example.com"
      - fill:
          selector: '[data-testid="phone-input"]'
          value: "555-2468"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "SecurePass123!"
      - fill:
          selector: '[data-testid="confirm-password-input"]'
          value: "SecurePass123!"
      - click: '[data-testid="register-button"]'
          
  - checkpoint: "Verify Registration Success"
    verifications:
      - expect:
          selector: '[data-testid="success-message"]'
          assertion: toContainText
          text: "Registration successful"
      - expect:
          selector: '[data-testid="activation-prompt"]'
          assertion: toContainText
          text: "Check your email"
          
  - checkpoint: "Retrieve Activation Email"
    actions:
      - checkEmail:
          to: "emma.johnson@example.com"
          subject: "Activate Your Account"
          extract_link: "activation_url"
          
  - checkpoint: "Activate Account"
    actions:
      - goto: "${activation_url}"
      - expect:
          selector: '[data-testid="activation-success"]'
          assertion: toContainText
          text: "Account activated"
          
  - checkpoint: "Login to Account"
    actions:
      - goto: "/login"
      - fill:
          selector: '[data-testid="email-input"]'
          value: "emma.johnson@example.com"
      - fill:
          selector: '[data-testid="password-input"]'
          value: "SecurePass123!"
      - click: '[data-testid="login-button"]'
          
  - checkpoint: "Verify Patient Dashboard Access"
    verifications:
      - expectURL: "/patient/dashboard"
      - expect:
          selector: '[data-testid="welcome-message"]'
          assertion: toContainText
          text: "Welcome, Emma"

validation_criteria:
  - user_created_in_database: true
  - activation_email_sent: true
  - account_status: "active"
  - login_successful: true
```

---

### Phase 2: Appointment Search & Booking

**Objective**: Patient searches for and books an appointment  
**Requirements**: FR-006, FR-007, FR-013  
**Use Case**: UC-001  
**Duration**: 2 minutes

```yaml
phase: "Appointment Booking"
steps:
  - checkpoint: "Navigate to Booking"
    actions:
      - click: '[data-testid="book-appointment-link"]'
      - expectURL: "/appointments/book"
      
  - checkpoint: "Search for Available Slots"
    actions:
      - select:
          selector: '[data-testid="provider-select"]'
          value: "Dr. Smith"
      - fill:
          selector: '[data-testid="date-picker"]'
          value: "2026-06-20"
      - click: '[data-testid="search-slots-button"]'
          
  - checkpoint: "Verify Slots Displayed"
    verifications:
      - expect:
          selector: '[data-testid="available-slots"]'
          assertion: toBeVisible
      - expect:
          selector: '[data-testid="slot-09-00-AM"]'
          assertion: toBeVisible
      - expect:
          selector: '[data-testid="slot-10-00-AM"]'
          assertion: toBeVisible
      - expect:
          selector: '[data-testid="slot-11-00-AM"]'
          assertion: not.toBeVisible  # Unavailable slot
          
  - checkpoint: "Select 2:00 PM Slot"
    actions:
      - click: '[data-testid="slot-02-00-PM"]'
      - expect:
          selector: '[data-testid="selected-slot"]'
          assertion: toContainText
          text: "2:00 PM"

validation_criteria:
  - search_results_accurate: true
  - unavailable_slots_hidden: true
  - slot_selection_confirmed: true
```

---

### Phase 3: Preferred Slot Registration

**Objective**: Patient registers preference for unavailable slot  
**Requirements**: FR-014  
**Use Case**: UC-001 Alternative Flow  
**Duration**: 1 minute

```yaml
phase: "Preferred Slot Registration"
steps:
  - checkpoint: "Expand Preferred Slot Options"
    actions:
      - click: '[data-testid="preferred-slot-toggle"]'
      - expect:
          selector: '[data-testid="preferred-slot-section"]'
          assertion: toBeVisible
          
  - checkpoint: "View All Slots Including Unavailable"
    actions:
      - click: '[data-testid="show-all-slots"]'
      - expect:
          selector: '[data-testid="slot-11-00-AM-unavailable"]'
          assertion: toBeVisible
          
  - checkpoint: "Select 11:00 AM as Preferred"
    actions:
      - click: '[data-testid="slot-11-00-AM-preferred"]'
      - expect:
          selector: '[data-testid="preferred-slot-selected"]'
          assertion: toContainText
          text: "11:00 AM"
          
  - checkpoint: "Confirm Booking with Preference"
    actions:
      - click: '[data-testid="confirm-booking"]'
      - expect:
          selector: '[data-testid="confirmation-message"]'
          assertion: toContainText
          text: "Appointment confirmed for 2:00 PM"
      - expect:
          selector: '[data-testid="preferred-slot-info"]'
          assertion: toContainText
          text: "You'll be notified if 11:00 AM becomes available"

validation_criteria:
  - appointment_booked_at_2pm: true
  - preferred_slot_11am_registered: true
  - monitoring_active: true
```

---

### Phase 4: Appointment Confirmation & PDF

**Objective**: Receive and verify appointment confirmation  
**Requirements**: FR-013  
**Duration**: 1 minute

```yaml
phase: "Appointment Confirmation"
steps:
  - checkpoint: "Verify Confirmation Page"
    verifications:
      - expect:
          selector: '[data-testid="appointment-details"]'
          assertion: toContainText
          text: "Dr. Smith"
      - expect:
          selector: '[data-testid="appointment-details"]'
          assertion: toContainText
          text: "June 20, 2026 at 2:00 PM"
          
  - checkpoint: "Download Confirmation PDF"
    actions:
      - click: '[data-testid="download-pdf"]'
      - waitForDownload:
          filename: "appointment-confirmation.pdf"
          
  - checkpoint: "Verify PDF Contents"
    verifications:
      - pdfContains:
          file: "appointment-confirmation.pdf"
          text: "Emma Johnson"
      - pdfContains:
          file: "appointment-confirmation.pdf"
          text: "Dr. Smith"
      - pdfContains:
          file: "appointment-confirmation.pdf"
          text: "2:00 PM"
          
  - checkpoint: "Verify Confirmation Email"
    verifications:
      - checkEmail:
          to: "emma.johnson@example.com"
          subject: "Appointment Confirmation"
          attachments: ["appointment-confirmation.pdf"]
      - emailContains:
          text: "Your appointment is confirmed"
      - emailContains:
          text: "June 20, 2026 at 2:00 PM"

validation_criteria:
  - pdf_generated: true
  - pdf_download_successful: true
  - confirmation_email_sent: true
  - email_has_pdf_attachment: true
```

---

### Phase 5: AI-Assisted Intake Completion

**Objective**: Complete patient intake via AI conversation  
**Requirements**: FR-027, FR-031  
**Use Case**: UC-005  
**Duration**: 3 minutes

```yaml
phase: "AI Intake Completion"
steps:
  - checkpoint: "Navigate to Intake"
    actions:
      - click: '[data-testid="complete-intake-link"]'
      - expectURL: "/intake"
      
  - checkpoint: "Select AI Mode"
    actions:
      - click: '[data-testid="ai-mode-button"]'
      - expect:
          selector: '[data-testid="ai-greeting"]'
          assertion: toBeVisible
          
  - checkpoint: "Respond to Medical History Question"
    actions:
      - waitForSelector: '[data-testid="ai-response"]:has-text("medical history")'
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I have seasonal allergies and occasional migraines"
      - click: '[data-testid="send-message"]'
          
  - checkpoint: "Respond to Medications Question"
    actions:
      - waitForSelector: '[data-testid="ai-response"]:has-text("medications")'
      - fill:
          selector: '[data-testid="message-input"]'
          value: "I take Zyrtec 10mg daily for allergies"
      - click: '[data-testid="send-message"]'
          
  - checkpoint: "Respond to Allergies Question"
    actions:
      - waitForSelector: '[data-testid="ai-response"]:has-text("allergies")'
      - fill:
          selector: '[data-testid="message-input"]'
          value: "No drug allergies, only environmental allergies to pollen"
      - click: '[data-testid="send-message"]'
          
  - checkpoint: "Respond to Symptoms Question"
    actions:
      - waitForSelector: '[data-testid="ai-response"]:has-text("symptoms")'
      - fill:
          selector: '[data-testid="message-input"]'
          value: "Currently experiencing seasonal allergy symptoms - sneezing and itchy eyes"
      - click: '[data-testid="send-message"]'
          
  - checkpoint: "Review Captured Data"
    verifications:
      - expect:
          selector: '[data-testid="summary-conditions"]'
          assertion: toContainText
          text: "Seasonal allergies"
      - expect:
          selector: '[data-testid="summary-medications"]'
          assertion: toContainText
          text: "Zyrtec"
      - expect:
          selector: '[data-testid="summary-allergies"]'
          assertion: toContainText
          text: "Pollen"
          
  - checkpoint: "Confirm Intake"
    actions:
      - click: '[data-testid="confirm-intake"]'
      - expect:
          selector: '[data-testid="intake-success"]'
          assertion: toContainText
          text: "Intake completed"

validation_criteria:
  - all_intake_fields_captured: true
  - conversational_parsing_accurate: true
  - intake_data_persisted: true
```

---

### Phase 6: Calendar Integration

**Objective**: Sync appointment to external calendar  
**Requirements**: FR-022  
**Use Case**: UC-015  
**Duration**: 1 minute

```yaml
phase: "Calendar Sync"
steps:
  - checkpoint: "Return to Appointment Details"
    actions:
      - click: '[data-testid="my-appointments"]'
      - click: '[data-testid="appointment-june-20"]'
          
  - checkpoint: "Connect Google Calendar"
    actions:
      - click: '[data-testid="sync-calendar"]'
      - select:
          selector: '[data-testid="calendar-provider"]'
          value: "Google Calendar"
      - click: '[data-testid="authorize-google"]'
          
  - checkpoint: "Complete OAuth Flow"
    actions:
      - expect:
          selector: '[data-testid="google-login"]'
          assertion: toBeVisible
      - fill:
          selector: '#google-email'
          value: "emma.johnson@gmail.com"
      - fill:
          selector: '#google-password'
          value: "GoogleTestPassword"
      - click: '#sign-in'
      - click: '#allow-access'
          
  - checkpoint: "Verify Sync Success"
    verifications:
      - expect:
          selector: '[data-testid="calendar-sync-success"]'
          assertion: toContainText
          text: "Synced to Google Calendar"
      - apiRequest:
          method: GET
          url: "https://www.googleapis.com/calendar/v3/calendars/primary/events"
          headers:
            Authorization: "Bearer ${google_token}"
          expect:
            body:
              summary: "Appointment with Dr. Smith"
              start:
                dateTime: "2026-06-20T14:00:00"

validation_criteria:
  - calendar_oauth_completed: true
  - event_created_in_google_calendar: true
  - calendar_link_stored: true
```

---

### Phase 7: Appointment Reminders

**Objective**: Receive appointment reminders  
**Requirements**: FR-018, FR-019  
**Use Case**: UC-011  
**Duration**: Automated (time-triggered)

```yaml
phase: "Reminder Delivery"
note: "This phase is time-triggered and validated via background jobs"

steps:
  - checkpoint: "24-Hour Reminder Sent"
    time: "2026-06-19 14:00:00"  # 24 hours before
    verifications:
      - checkEmail:
          to: "emma.johnson@example.com"
          subject: "Appointment Reminder - Tomorrow"
          body: "You have an appointment tomorrow at 2:00 PM"
      - checkSMS:
          to: "555-2468"
          message: "Reminder: Appointment with Dr. Smith tomorrow at 2:00 PM"
          
  - checkpoint: "2-Hour Reminder Sent"
    time: "2026-06-20 12:00:00"  # 2 hours before
    verifications:
      - checkEmail:
          to: "emma.johnson@example.com"
          subject: "Appointment Reminder - Today"
      - checkSMS:
          to: "555-2468"
          message: "Reminder: Your appointment is in 2 hours"

validation_criteria:
  - email_reminders_sent: true
  - sms_reminders_sent: true
  - reminder_timing_accurate: true
```

---

### Phase 8: Appointment Day - Arrival

**Objective**: Patient arrives, staff marks attendance  
**Requirements**: FR-011  
**Use Case**: UC-014  
**Duration**: 1 minute

```yaml
phase: "Patient Arrival"
context: "Staff session"

steps:
  - checkpoint: "Staff Views Today's Schedule"
    actions:
      - login_as: "Staff"
      - goto: "/staff/schedule"
      - expect:
          selector: '[data-testid="schedule-june-20"]'
          assertion: toContainText
          text: "Emma Johnson - 2:00 PM"
          
  - checkpoint: "Patient Arrives at Clinic"
    actions:
      - click: '[data-testid="patient-emma-johnson"]'
      - expect:
          selector: '[data-testid="patient-status"]'
          assertion: toContainText
          text: "Scheduled"
          
  - checkpoint: "Staff Marks Patient as Arrived"
    actions:
      - click: '[data-testid="mark-arrived"]'
      - expect:
          selector: '[data-testid="confirmation-dialog"]'
          assertion: toContainText
          text: "Mark Emma Johnson as arrived?"
      - click: '[data-testid="confirm-arrived"]'
          
  - checkpoint: "Verify Arrival Recorded"
    verifications:
      - expect:
          selector: '[data-testid="patient-status"]'
          assertion: toContainText
          text: "Arrived"
      - expect:
          selector: '[data-testid="arrival-time"]'
          assertion: toBeVisible
      - apiRequest:
          method: GET
          url: "/api/appointments/${appointment.id}"
          expect:
            body:
              status: "arrived"
              arrival_timestamp: "${current_timestamp}"

validation_criteria:
  - patient_marked_arrived: true
  - arrival_timestamp_recorded: true
  - audit_log_entry_created: true
```

---

## Cross-Phase Validation

```yaml
end_to_end_validations:
  - name: "Complete Patient Record"
    verify:
      - user_account_exists: true
      - appointment_booked: true
      - intake_completed: true
      - calendar_synced: true
      - arrival_logged: true
      
  - name: "Notification Trail"
    verify:
      - activation_email: received
      - confirmation_email: received
      - 24h_reminder_email: received
      - 2h_reminder_email: received
      - 24h_reminder_sms: received
      - 2h_reminder_sms: received
      
  - name: "Data Integrity"
    verify:
      - patient_demographics: complete
      - appointment_details: accurate
      - intake_data: structured
      - external_calendar_event: created
      - audit_trail: complete
```

---

## Success Criteria

| Criteria | Target | Actual |
|----------|--------|--------|
| Journey Completion Time | <10 minutes | _measure_ |
| All Phases Passed | 100% | _measure_ |
| Email Delivery Rate | 100% | _measure_ |
| SMS Delivery Rate | 100% | _measure_ |
| Calendar Sync Success | 100% | _measure_ |
| Data Accuracy | 100% | _measure_ |
| Zero Errors | True | _measure_ |

---

## Error Scenarios & Recovery

```yaml
error_handling:
  - scenario: "Email Delivery Failure"
    phase: "Registration"
    recovery:
      - retry_email_send: 3_attempts
      - fallback_to_manual_activation: true
      
  - scenario: "Calendar API Unavailable"
    phase: "Calendar Sync"
    recovery:
      - graceful_degradation: true
      - appointment_still_valid: true
      - retry_mechanism: background_job
      
  - scenario: "AI Service Timeout"
    phase: "Intake"
    recovery:
      - fallback_to_manual_mode: true
      - preserve_partial_data: true
```

---

## Traceability Matrix

| Phase | Requirements | Use Cases | Status |
|-------|--------------|-----------|--------|
| 1. Registration | FR-001 | - | ✅ |
| 2. Booking | FR-006, FR-007, FR-013 | UC-001 | ✅ |
| 3. Preferred Slot | FR-014 | UC-001 | ✅ |
| 4. Confirmation | FR-013 | UC-001 | ✅ |
| 5. Intake | FR-027, FR-031 | UC-005 | ✅ |
| 6. Calendar | FR-022 | UC-015 | ✅ |
| 7. Reminders | FR-018, FR-019 | UC-011 | ✅ |
| 8. Arrival | FR-011 | UC-014 | ✅ |

---

## Test Data

```yaml
test_user:
  name: "Emma Johnson"
  email: "emma.johnson@example.com"
  phone: "555-2468"
  password: "SecurePass123!"
  
appointment_details:
  provider: "Dr. Smith"
  date: "2026-06-20"
  time: "02:00 PM"
  preferred_time: "11:00 AM"
  
intake_data:
  conditions: "Seasonal allergies, occasional migraines"
  medications: "Zyrtec 10mg daily"
  allergies: "Pollen (environmental only)"
  symptoms: "Sneezing, itchy eyes"
```

---

**Generated**: 2026-06-10  
**Source**: MasterTestPlan.md  
**Framework**: Playwright + TypeScript  
**Estimated Execution Time**: 8-10 minutes
