# Test Workflow: Appointment Booking & Management

## Metadata

| Field | Value |
|-------|-------|
| **Feature** | Appointment Booking & Management |
| **Test Type** | Feature-level |
| **Requirements** | FR-006 to FR-017 |
| **Use Cases** | UC-001, UC-002, UC-003, UC-004, UC-014 |
| **Priority** | Critical |
| **Test Framework** | Playwright + TypeScript |

---

## Feature Overview

This test workflow validates the appointment booking system including:
- Patient searches and books appointments (FR-006, FR-007)
- Staff books appointments for patients (FR-008)
- Walk-in registrations and same-day queue (FR-009, FR-010)
- Patient arrival tracking (FR-011)
- Preferred slot swap functionality (FR-014 to FR-017)
- Appointment confirmations (FR-013)

---

## Page Objects Required

```yaml
pages:
  - AppointmentSearchPage:
      selectors:
        providerDropdown: '[data-testid="provider-select"]'
        datePicker: '[data-testid="date-picker"]'
        searchButton: '[data-testid="search-slots-button"]'
        slotsContainer: '[data-testid="available-slots"]'
        slotCard: '[data-testid^="slot-"]'
        preferredSlotCheckbox: '[data-testid="preferred-slot-checkbox"]'
        
  - AppointmentConfirmationPage:
      selectors:
        confirmationMessage: '[data-testid="confirmation-message"]'
        appointmentDetails: '[data-testid="appointment-details"]'
        downloadPDFButton: '[data-testid="download-pdf"]'
        calendarSyncButton: '[data-testid="sync-calendar"]'
        
  - StaffBookingPage:
      selectors:
        patientSearchInput: '[data-testid="patient-search"]'
        createPatientButton: '[data-testid="create-patient"]'
        providerSelect: '[data-testid="provider-select"]'
        slotsList: '[data-testid="slots-list"]'
        bookButton: '[data-testid="book-appointment"]'
        
  - WalkInPage:
      selectors:
        walkInButton: '[data-testid="walk-in-booking"]'
        patientNameInput: '[data-testid="patient-name"]'
        phoneInput: '[data-testid="phone-number"]'
        addToQueueButton: '[data-testid="add-to-queue"]'
        queueList: '[data-testid="same-day-queue"]'
        markArrivedButton: '[data-testid="mark-arrived"]'
        
  - PatientDashboardPage:
      selectors:
        upcomingAppointments: '[data-testid="upcoming-appointments"]'
        appointmentCard: '[data-testid^="appointment-"]'
        cancelButton: '[data-testid="cancel-appointment"]'
```

---

## Test Cases

### Happy Path

#### TW-APPT-001: Patient Searches and Books Available Slot

**Requirement**: FR-006, FR-007, FR-013  
**Use Case**: UC-001  
**Priority**: Critical

```yaml
test: "Patient Successfully Books Appointment"
preconditions:
  - authenticated_as: "Patient"
  - available_slots:
      provider: "Dr. Smith"
      date: "2026-06-15"
      slots: ["09:00 AM", "10:00 AM", "02:00 PM"]
      
steps:
  - step: "Navigate to appointment booking"
    action: goto
    url: "/appointments/book"
    
  - step: "Select provider"
    action: select
    selector: '[data-testid="provider-select"]'
    value: "Dr. Smith"
    
  - step: "Select date"
    action: fill
    selector: '[data-testid="date-picker"]'
    value: "2026-06-15"
    
  - step: "Click search"
    action: click
    selector: '[data-testid="search-slots-button"]'
    
  - step: "Verify available slots displayed"
    action: expect
    selector: '[data-testid="available-slots"]'
    assertion: toContainText
    text: "09:00 AM"
    
  - step: "Select 10:00 AM slot"
    action: click
    selector: '[data-testid="slot-10-00-AM"]'
    
  - step: "Confirm booking"
    action: click
    selector: '[data-testid="confirm-booking"]'
    
  - step: "Verify confirmation message"
    action: expect
    selector: '[data-testid="confirmation-message"]'
    assertion: toContainText
    text: "Appointment confirmed"
    
  - step: "Verify appointment details displayed"
    actions:
      - expect:
          selector: '[data-testid="appointment-details"]'
          assertion: toContainText
          text: "Dr. Smith"
      - expect:
          selector: '[data-testid="appointment-details"]'
          assertion: toContainText
          text: "June 15, 2026 at 10:00 AM"
          
  - step: "Verify PDF download available"
    action: expect
    selector: '[data-testid="download-pdf"]'
    assertion: toBeVisible
    
  - step: "Verify confirmation email sent"
    action: checkEmail
    to: "${patient.email}"
    subject: "Appointment Confirmation"
    attachments: ["appointment-confirmation.pdf"]

checkpoints:
  - name: "Appointment Created"
    verify: "Database record exists"
  - name: "Slot Reserved"
    verify: "Slot no longer available to others"
  - name: "PDF Generated"
    verify: "Confirmation PDF created"
  - name: "Email Sent"
    verify: "Confirmation email in outbox"
```

---

#### TW-APPT-002: Patient Books With Preferred Slot Selection

**Requirement**: FR-014  
**Use Case**: UC-001 Alternative Flow  
**Priority**: High

```yaml
test: "Patient Registers Preferred Slot While Booking"
preconditions:
  - authenticated_as: "Patient"
  - available_slots: ["02:00 PM"]
  - unavailable_slots: ["10:00 AM"]
  
steps:
  - step: "Search for appointments"
    actions:
      - goto: "/appointments/book"
      - select:
          selector: '[data-testid="provider-select"]'
          value: "Dr. Smith"
      - fill:
          selector: '[data-testid="date-picker"]'
          value: "2026-06-15"
      - click: '[data-testid="search-slots-button"]'
          
  - step: "Select available 2:00 PM slot"
    action: click
    selector: '[data-testid="slot-02-00-PM"]'
    
  - step: "Expand preferred slot options"
    action: click
    selector: '[data-testid="preferred-slot-section"]'
    
  - step: "Select 10:00 AM as preferred slot"
    actions:
      - click: '[data-testid="show-all-slots"]'
      - click: '[data-testid="slot-10-00-AM-preferred"]'
      - check: '[data-testid="preferred-slot-checkbox"]'
          
  - step: "Confirm booking with preference"
    action: click
    selector: '[data-testid="confirm-booking"]'
    
  - step: "Verify booking confirmation"
    action: expect
    selector: '[data-testid="appointment-details"]'
    assertion: toContainText
    text: "2:00 PM"
    
  - step: "Verify preferred slot registered"
    action: expect
    selector: '[data-testid="preferred-slot-info"]'
    assertion: toContainText
    text: "Preferred: 10:00 AM"

checkpoints:
  - name: "Appointment at 2:00 PM"
    verify: "Active appointment booked"
  - name: "Preference Registered"
    verify: "Preferred slot saved in database"
  - name: "Monitoring Active"
    verify: "System monitors 10:00 AM slot availability"
```

---

#### TW-APPT-003: Staff Books Appointment for Patient

**Requirement**: FR-008  
**Use Case**: UC-002  
**Priority**: Critical

```yaml
test: "Staff Books Appointment on Behalf of Patient"
preconditions:
  - authenticated_as: "Staff"
  - existing_patient:
      name: "John Doe"
      email: "john.doe@example.com"
      
steps:
  - step: "Navigate to staff booking"
    action: goto
    url: "/staff/bookings"
    
  - step: "Search for patient"
    actions:
      - fill:
          selector: '[data-testid="patient-search"]'
          value: "John Doe"
      - click: '[data-testid="search-patient"]'
          
  - step: "Select patient from results"
    action: click
    selector: '[data-testid="patient-result-john-doe"]'
    
  - step: "Select provider and date"
    actions:
      - select:
          selector: '[data-testid="provider-select"]'
          value: "Dr. Smith"
      - fill:
          selector: '[data-testid="date-picker"]'
          value: "2026-06-16"
          
  - step: "Select available slot"
    action: click
    selector: '[data-testid="slot-11-00-AM"]'
    
  - step: "Book appointment"
    action: click
    selector: '[data-testid="book-appointment"]'
    
  - step: "Verify booking confirmation"
    action: expect
    selector: '[data-testid="success-message"]'
    assertion: toContainText
    text: "Appointment booked for John Doe"
    
  - step: "Verify patient receives confirmation email"
    action: checkEmail
    to: "john.doe@example.com"
    subject: "Appointment Confirmation"

checkpoints:
  - name: "Appointment Linked to Patient"
    verify: "Appointment record has correct patient_id"
  - name: "Patient Notified"
    verify: "Email sent to patient, not staff"
```

---

#### TW-APPT-004: Staff Registers Walk-In and Marks Arrived

**Requirement**: FR-009, FR-010, FR-011  
**Use Cases**: UC-003, UC-014  
**Priority**: High

```yaml
test: "Walk-In Registration and Arrival Tracking"
preconditions:
  - authenticated_as: "Staff"
  
steps:
  - step: "Navigate to walk-in section"
    action: goto
    url: "/staff/walk-ins"
    
  - step: "Click walk-in button"
    action: click
    selector: '[data-testid="walk-in-booking"]'
    
  - step: "Search for existing patient"
    actions:
      - fill:
          selector: '[data-testid="patient-search"]'
          value: "Jane Smith"
      - click: '[data-testid="search-button"]'
          
  - step: "Patient not found - create new"
    actions:
      - click: '[data-testid="create-patient"]'
      - fill:
          selector: '[data-testid="patient-name"]'
          value: "Jane Smith"
      - fill:
          selector: '[data-testid="phone-number"]'
          value: "555-9876"
      - fill:
          selector: '[data-testid="email"]'
          value: "jane.smith@example.com"
          
  - step: "Add to same-day queue"
    action: click
    selector: '[data-testid="add-to-queue"]'
    
  - step: "Verify walk-in added to queue"
    action: expect
    selector: '[data-testid="same-day-queue"] >> text=Jane Smith'
    assertion: toBeVisible
    
  - step: "Verify timestamp recorded"
    action: expect
    selector: '[data-testid="walk-in-timestamp"]'
    assertion: toContainText
    text: "${current_time}"
    
  - step: "Mark patient as arrived"
    actions:
      - click: '[data-testid="patient-jane-smith"]'
      - click: '[data-testid="mark-arrived"]'
          
  - step: "Verify arrival status updated"
    action: expect
    selector: '[data-testid="patient-status-jane-smith"]'
    assertion: toContainText
    text: "Arrived"

checkpoints:
  - name: "Walk-In Record Created"
    verify: "Appointment type is 'walk-in'"
  - name: "Queue Position Assigned"
    verify: "Patient in same-day queue"
  - name: "Arrival Logged"
    verify: "Arrival timestamp recorded"
```

---

#### TW-APPT-005: Automatic Preferred Slot Swap

**Requirement**: FR-015, FR-016, FR-017  
**Use Case**: UC-004  
**Priority**: High

```yaml
test: "System Automatically Swaps to Preferred Slot"
preconditions:
  - patient_A:
      email: "patient-a@example.com"
      appointment:
        time: "02:00 PM"
        date: "2026-06-15"
      preferred_slot: "10:00 AM"
      
steps:
  - step: "Verify initial appointment at 2:00 PM"
    action: apiRequest
    method: GET
    url: "/api/appointments/${patient_A.appointment.id}"
    expect:
      status: 200
      body:
        time: "02:00 PM"
        
  - step: "Different patient cancels 10:00 AM"
    context: patient_B
    actions:
      - goto: "/appointments"
      - click: '[data-testid="cancel-appointment-10-00-AM"]'
      - click: '[data-testid="confirm-cancel"]'
          
  - step: "Wait for swap processing"
    action: wait
    duration: 2000
    
  - step: "Verify Patient A appointment updated to 10:00 AM"
    action: apiRequest
    method: GET
    url: "/api/appointments/${patient_A.appointment.id}"
    expect:
      status: 200
      body:
        time: "10:00 AM"
        
  - step: "Verify 2:00 PM slot released"
    action: apiRequest
    method: GET
    url: "/api/slots?date=2026-06-15&time=02:00 PM"
    expect:
      status: 200
      body:
        available: true
        
  - step: "Verify SMS notification sent"
    action: checkSMS
    to: "${patient_A.phone}"
    message: "Your appointment has been moved to your preferred time: 10:00 AM"
    
  - step: "Verify email notification sent"
    action: checkEmail
    to: "patient-a@example.com"
    subject: "Appointment Updated - Preferred Slot Available"

checkpoints:
  - name: "Appointment Swapped"
    verify: "Patient A now has 10:00 AM slot"
  - name: "Original Slot Released"
    verify: "2:00 PM is available for booking"
  - name: "Notifications Sent"
    verify: "SMS and email delivered"
```

---

### Edge Cases

#### TW-APPT-006: Concurrent Booking Attempts

**Requirement**: FR-007  
**Priority**: High

```yaml
test: "Only One Patient Books Same Slot (Race Condition)"
preconditions:
  - available_slot:
      provider: "Dr. Smith"
      date: "2026-06-15"
      time: "10:00 AM"
      
steps:
  - step: "Patient A and Patient B both select same slot"
    parallel:
      - context: patient_A
        actions:
          - goto: "/appointments/book"
          - select:
              selector: '[data-testid="provider-select"]'
              value: "Dr. Smith"
          - fill:
              selector: '[data-testid="date-picker"]'
              value: "2026-06-15"
          - click: '[data-testid="search-slots-button"]'
          - click: '[data-testid="slot-10-00-AM"]'
          - click: '[data-testid="confirm-booking"]'
              
      - context: patient_B
        actions:
          - goto: "/appointments/book"
          - select:
              selector: '[data-testid="provider-select"]'
              value: "Dr. Smith"
          - fill:
              selector: '[data-testid="date-picker"]'
              value: "2026-06-15"
          - click: '[data-testid="search-slots-button"]'
          - click: '[data-testid="slot-10-00-AM"]'
          - click: '[data-testid="confirm-booking"]'
              
  - step: "Verify only one booking succeeded"
    action: apiRequest
    method: GET
    url: "/api/appointments?slot=10-00-AM&date=2026-06-15"
    expect:
      status: 200
      body:
        count: 1
        
  - step: "Verify second patient sees error"
    context: "patient_B OR patient_A"
    action: expect
    selector: '[data-testid="error-message"]'
    assertion: toContainText
    text: "This slot is no longer available"

checkpoints:
  - name: "No Double Booking"
    verify: "Only one appointment for slot"
  - name: "Failed User Notified"
    verify: "Error message displayed"
```

---

#### TW-APPT-007: Multiple Patients Prefer Same Slot - FIFO Priority

**Requirement**: FR-015  
**Use Case**: UC-004 Alternative Flow  
**Priority**: Medium

```yaml
test: "Earliest Registrant Gets Swap (FIFO)"
preconditions:
  - patient_A:
      preferred_slot: "09:00 AM"
      registered_at: "2026-06-10 10:00:00"
  - patient_B:
      preferred_slot: "09:00 AM"
      registered_at: "2026-06-10 12:30:00"  # Later than A
      
steps:
  - step: "Another patient cancels 09:00 AM"
    context: patient_C
    actions:
      - goto: "/appointments"
      - click: '[data-testid="cancel-appointment-09-00-AM"]'
      - click: '[data-testid="confirm-cancel"]'
          
  - step: "Wait for swap processing"
    action: wait
    duration: 2000
    
  - step: "Verify Patient A got the slot (earliest)"
    action: apiRequest
    method: GET
    url: "/api/appointments?patient=${patient_A.id}"
    expect:
      body:
        time: "09:00 AM"
        
  - step: "Verify Patient B still has original time"
    action: apiRequest
    method: GET
    url: "/api/appointments?patient=${patient_B.id}"
    expect:
      body:
        time: "${patient_B.original_time}"
        preferred_slot: "09:00 AM"  # Preference still active

checkpoints:
  - name: "FIFO Priority Honored"
    verify: "Earliest registrant received swap"
  - name: "Other Preferences Active"
    verify: "Patient B preference still monitored"
```

---

### Error Scenarios

#### TW-APPT-008: Patient Attempts Self-Check-In (Forbidden)

**Requirement**: FR-012  
**Priority**: High

```yaml
test: "Patient Cannot Self-Check-In"
preconditions:
  - authenticated_as: "Patient"
  - upcoming_appointment:
      date: "2026-06-15"
      time: "10:00 AM"
      
steps:
  - step: "Navigate to appointments"
    action: goto
    url: "/appointments"
    
  - step: "Verify no check-in button displayed"
    action: expect
    selector: '[data-testid="check-in-button"]'
    assertion: toBeHidden
    
  - step: "Verify no QR code option"
    action: expect
    selector: '[data-testid="qr-check-in"]'
    assertion: toBeHidden
    
  - step: "Attempt direct API call to check-in"
    action: apiRequest
    method: POST
    url: "/api/appointments/${appointment.id}/check-in"
    expect:
      status: 403
      body:
        error: "Patients cannot self-check-in"

checkpoints:
  - name: "UI Check-In Hidden"
    verify: "No check-in controls visible"
  - name: "API Blocked"
    verify: "403 returned for patient check-in attempt"
```

---

#### TW-APPT-009: Booking With No Available Slots

**Requirement**: FR-006  
**Use Case**: UC-001 Alternative Flow  
**Priority**: Medium

```yaml
test: "No Slots Available - Waitlist Option"
preconditions:
  - authenticated_as: "Patient"
  - all_slots_booked:
      provider: "Dr. Smith"
      date: "2026-06-15"
      
steps:
  - step: "Search for appointments"
    actions:
      - goto: "/appointments/book"
      - select:
          selector: '[data-testid="provider-select"]'
          value: "Dr. Smith"
      - fill:
          selector: '[data-testid="date-picker"]'
          value: "2026-06-15"
      - click: '[data-testid="search-slots-button"]'
          
  - step: "Verify no available slots message"
    action: expect
    selector: '[data-testid="no-slots-message"]'
    assertion: toContainText
    text: "No appointments available"
    
  - step: "Verify waitlist option displayed"
    action: expect
    selector: '[data-testid="join-waitlist"]'
    assertion: toBeVisible

checkpoints:
  - name: "Empty State Handled"
    verify: "Clear message displayed"
  - name: "Alternative Offered"
    verify: "Waitlist option available"
```

---

## Test Data

### Providers and Availability

```yaml
providers:
  - name: "Dr. Smith"
    specialty: "General Practice"
    available_dates: ["2026-06-15", "2026-06-16", "2026-06-17"]
    slots_per_day:
      - "09:00 AM"
      - "10:00 AM"
      - "11:00 AM"
      - "02:00 PM"
      - "03:00 PM"
      - "04:00 PM"
```

### Test Patients

```yaml
test_patients:
  - name: "John Doe"
    email: "john.doe@example.com"
    phone: "555-0123"
    role: "Patient"
    
  - name: "Jane Smith"
    email: "jane.smith@example.com"
    phone: "555-9876"
    role: "Patient"
```

---

## Traceability Matrix

| Test Case | Requirements | Use Cases | Priority |
|-----------|--------------|-----------|----------|
| TW-APPT-001 | FR-006, FR-007, FR-013 | UC-001 | Critical |
| TW-APPT-002 | FR-014 | UC-001 | High |
| TW-APPT-003 | FR-008 | UC-002 | Critical |
| TW-APPT-004 | FR-009, FR-010, FR-011 | UC-003, UC-014 | High |
| TW-APPT-005 | FR-015, FR-016, FR-017 | UC-004 | High |
| TW-APPT-006 | FR-007 | - | High |
| TW-APPT-007 | FR-015 | UC-004 | Medium |
| TW-APPT-008 | FR-012 | - | High |
| TW-APPT-009 | FR-006 | UC-001 | Medium |

---

## Execution Notes

- Use database seeding for provider availability
- Mock email and SMS services for notification verification
- Implement wait strategies for asynchronous slot swap processing
- Test concurrent scenarios with separate browser contexts
- Clear appointment data between test runs

---

**Generated**: 2026-06-10  
**Source**: MasterTestPlan.md  
**Framework**: Playwright + TypeScript
